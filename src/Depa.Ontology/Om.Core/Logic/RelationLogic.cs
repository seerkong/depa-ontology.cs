using Depa.Cozo;
using Depa.Ontology.Contracts.Models;
using Depa.Ontology.Inputs;
using Depa.Ontology.Internals;
using Depa.Ontology.Runtime;

namespace Depa.Ontology.Logic;

internal static class RelationLogic
{
    public static async Task ValidateRelationAsync(
        CozoOmRuntime runtime,
        string fromId,
        string relationName,
        string toId,
        CancellationToken cancellationToken = default)
    {
        var relation = await ClassLogic.GetRelationDefinitionAsync(runtime, relationName, cancellationToken);
        var fromClass = await ObjectLogic.GetObjectClassAsync(runtime, fromId, cancellationToken);
        var toClass = await ObjectLogic.GetObjectClassAsync(runtime, toId, cancellationToken);
        var forward = await ClassLogic.IsSubclassOfAsync(runtime, fromClass, relation.FromClass, cancellationToken) &&
                      await ClassLogic.IsSubclassOfAsync(runtime, toClass, relation.ToClass, cancellationToken);
        var reverse = !relation.Directed &&
                      await ClassLogic.IsSubclassOfAsync(runtime, fromClass, relation.ToClass, cancellationToken) &&
                      await ClassLogic.IsSubclassOfAsync(runtime, toClass, relation.FromClass, cancellationToken);
        if (!forward && !reverse)
        {
            throw new CozoException($"Relation '{relation.RelationName}' cannot link '{fromClass}' -> '{toClass}'");
        }
    }

    public static async Task CreateRelationLinkAsync(CozoOmRuntime runtime, CreateRelationLinkInput input, CancellationToken cancellationToken = default)
    {
        var fromObjectId = OmConvert.RequireName(input.FromObjectId, nameof(input.FromObjectId));
        var toObjectId = OmConvert.RequireName(input.ToObjectId, nameof(input.ToObjectId));
        var relationName = await ClassLogic.ResolveRelationAsync(runtime, input.RelationName, cancellationToken);
        await ValidateRelationAsync(runtime, fromObjectId, relationName, toObjectId, cancellationToken);

        if (input.Options?.SkipConstraints == true)
        {
            await WriteRelationLinkAsync(runtime, fromObjectId, relationName, toObjectId, input.Payload, input.Options, cancellationToken);
            return;
        }

        await using var tx = await runtime.Store.BeginTransactionAsync(write: true, cancellationToken);
        var txRuntime = runtime with { Store = tx };
        await WriteRelationLinkAsync(txRuntime, fromObjectId, relationName, toObjectId, input.Payload, input.Options, cancellationToken);
        var validation = await ConstraintLogic.ValidateConstraintsAsync(txRuntime, fromObjectId, ["cross-entity"], cancellationToken);
        if (!validation.Valid)
        {
            throw new CozoException(string.Join("; ", validation.Errors));
        }

        await tx.CommitAsync(cancellationToken);
    }

    private static async Task WriteRelationLinkAsync(
        CozoOmRuntime runtime,
        string fromObjectId,
        string relationName,
        string toObjectId,
        object? payload,
        WriteOptions? options,
        CancellationToken cancellationToken)
    {
        var validTime = string.IsNullOrWhiteSpace(options?.ValidTime) ? "ASSERT" : options!.ValidTime!;
        var txTime = runtime.Options.TimeProvider.GetUtcNow().UtcDateTime.ToString("O");

        await runtime.Store.RunAsync(
            """
            ?[from_object_id, relation_name, to_object_id, valid_time, payload, tx_time] <- [[$from_object_id, $relation_name, $to_object_id, $valid_time, $payload, $tx_time]]
            :put om_relation_link {from_object_id, relation_name, to_object_id, valid_time => payload, tx_time}
            """,
            LogicSupport.Params(
                    ("from_object_id", fromObjectId),
                    ("relation_name", relationName),
                    ("to_object_id", toObjectId),
                    ("valid_time", validTime),
                    ("payload", payload ?? new Dictionary<string, object?>()),
                    ("tx_time", txTime)),
            cancellationToken: cancellationToken);
    }

    public static async Task RetractRelationLinkAsync(
        CozoOmRuntime runtime,
        string fromObjectId,
        string relationName,
        string toObjectId,
        WriteOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var canonicalRelation = await ClassLogic.ResolveRelationAsync(runtime, relationName, cancellationToken);
        await ValidateRelationAsync(runtime, fromObjectId, canonicalRelation, toObjectId, cancellationToken);
        var requested = options?.ValidTime;
        var validTime = string.IsNullOrWhiteSpace(requested)
            ? "RETRACT"
            : requested!.StartsWith('~') ? requested : $"~{requested}";
        var txTime = runtime.Options.TimeProvider.GetUtcNow().UtcDateTime.ToString("O");

        await runtime.Store.RunAsync(
            """
            ?[from_object_id, relation_name, to_object_id, valid_time, payload, tx_time] <- [[$from_object_id, $relation_name, $to_object_id, $valid_time, {}, $tx_time]]
            :put om_relation_link {from_object_id, relation_name, to_object_id, valid_time => payload, tx_time}
            """,
            LogicSupport.Params(
                ("from_object_id", fromObjectId),
                ("relation_name", canonicalRelation),
                ("to_object_id", toObjectId),
                ("valid_time", validTime),
                ("tx_time", txTime)),
            cancellationToken: cancellationToken);
    }

    public static async Task<NeighborResult> GetNeighborsAsync(
        CozoOmRuntime runtime,
        string objectId,
        string? relationName = null,
        OmDirection direction = OmDirection.Both,
        CancellationToken cancellationToken = default)
    {
        return await GetNeighborsAtAsync(runtime, objectId, relationName, direction, "\"NOW\"", null, cancellationToken);
    }

    public static async Task<NeighborResult> GetNeighborsAsOfAsync(
        CozoOmRuntime runtime,
        string objectId,
        string? relationName,
        string asOf,
        OmDirection direction = OmDirection.Both,
        CancellationToken cancellationToken = default)
    {
        var timestamp = OmConvert.NormalizeTimestamp(asOf, nameof(asOf));
        return await GetNeighborsAtAsync(runtime, objectId, relationName, direction, "$as_of", timestamp, cancellationToken);
    }

    public static async Task<IReadOnlyList<OmObjectRow>> TraverseAsync(
        CozoOmRuntime runtime,
        string startObjectId,
        IReadOnlyList<string>? relationPath,
        CancellationToken cancellationToken = default)
    {
        var start = OmConvert.RequireName(startObjectId, nameof(startObjectId));
        if (relationPath is null || relationPath.Count == 0)
        {
            var view = await ObjectLogic.GetObjectViewRowAsync(runtime, start, cancellationToken);
            return view is null ? [] : [new OmObjectRow(view.Id, view.ClassName, view.Label)];
        }

        var frontier = new[] { start };
        IReadOnlyList<OmObjectRow> level = [];
        foreach (var relation in relationPath)
        {
            var relationName = OmConvert.RequireName(relation, nameof(relationPath));
            var next = new Dictionary<string, OmObjectRow>(StringComparer.Ordinal);
            foreach (var objectId in frontier)
            {
                var neighbors = await GetNeighborsAsync(
                    runtime,
                    objectId,
                    relationName,
                    OmDirection.Outgoing,
                    cancellationToken);
                foreach (var neighbor in neighbors.Outgoing)
                {
                    next.TryAdd(neighbor.ObjectId, new OmObjectRow(neighbor.ObjectId, neighbor.ClassName, neighbor.Label));
                }
            }

            level = next.Values.OrderBy(entity => entity.Id, StringComparer.Ordinal).ToArray();
            if (level.Count == 0) return level;
            frontier = level.Select(entity => entity.Id).ToArray();
        }

        return level;
    }

    internal static Task<NeighborResult> GetNeighborsAtNormalizedAsOfAsync(
        CozoOmRuntime runtime,
        string objectId,
        string? relationName,
        string normalizedAsOf,
        OmDirection direction = OmDirection.Both,
        CancellationToken cancellationToken = default) =>
        GetNeighborsAtAsync(runtime, objectId, relationName, direction, "$as_of", normalizedAsOf, cancellationToken);

    public static async Task<IReadOnlyList<RelationLinkHistoryEntry>> GetRelationLinkHistoryAsync(
        CozoOmRuntime runtime,
        string fromObjectId,
        string relationName,
        string? toObjectId = null,
        HistoryRangeOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var filters = string.IsNullOrWhiteSpace(toObjectId) ? "" : ",\n  to_object_id = $to_object_id";
        var parameters = LogicSupport.Params(
            ("from_object_id", OmConvert.RequireName(fromObjectId, nameof(fromObjectId))),
            ("relation_name", await ClassLogic.ResolveRelationAsync(runtime, relationName, cancellationToken)));
        if (!string.IsNullOrWhiteSpace(toObjectId)) parameters["to_object_id"] = toObjectId;

        var result = await runtime.Store.RunAsync(
            "?[to_object_id, payload, valid_time, tx_time, is_assert] :=\n" +
            "  *om_relation_link{ from_object_id: $from_object_id, relation_name: $relation_name, to_object_id, valid_time, payload, tx_time },\n" +
            $"  is_assert = to_bool(valid_time){filters}\n" +
            ":sort valid_time, to_object_id",
            parameters,
            cancellationToken: cancellationToken);
        return result.Rows
            .Select(row => new RelationLinkHistoryEntry(
                fromObjectId,
                relationName,
                JsonRows.StringAt(row, 0) ?? "",
                JsonRows.ElementAt(row, 1),
                JsonRows.StringAt(row, 2) ?? row[2].ToString(),
                JsonRows.StringAt(row, 3) ?? "",
                JsonRows.BoolAt(row, 4)))
            .ToArray();
    }

    private static async Task<NeighborResult> GetNeighborsAtAsync(
        CozoOmRuntime runtime,
        string objectId,
        string? relationName,
        OmDirection direction,
        string atExpression,
        string? asOf,
        CancellationToken cancellationToken)
    {
        var id = OmConvert.RequireName(objectId, nameof(objectId));
        var relFilter = "";
        var parameters = LogicSupport.Params(("id", id));
        if (asOf is not null) parameters["as_of"] = asOf;
        if (!string.IsNullOrWhiteSpace(relationName))
        {
            parameters["relation_name"] = await ClassLogic.ResolveRelationAsync(runtime, relationName!, cancellationToken);
            relFilter = ",\n  relation_name = $relation_name";
        }

        var outgoing = direction is OmDirection.Outgoing or OmDirection.Both
            ? await NeighborRowsAsync(runtime, OutgoingScript(atExpression, relFilter), parameters, cancellationToken)
            : [];
        var incoming = direction is OmDirection.Incoming or OmDirection.Both
            ? await NeighborRowsAsync(runtime, IncomingScript(atExpression, relFilter), parameters, cancellationToken)
            : [];
        return new NeighborResult(outgoing, incoming);
    }

    private static string OutgoingScript(string atExpression, string relFilter)
    {
        return "?[relation_name, node_id, node_type, node_label] :=\n" +
               $"  *om_relation_link{{ from_object_id: $id, relation_name, to_object_id: node_id, payload: _payload @ {atExpression} }},\n" +
               $"  *om_object{{ id: node_id, class_name: node_type, label: node_label }}{relFilter}\n" +
               ":sort relation_name, node_id";
    }

    private static string IncomingScript(string atExpression, string relFilter)
    {
        return "?[relation_name, node_id, node_type, node_label] :=\n" +
               $"  *om_relation_link{{ from_object_id: node_id, relation_name, to_object_id: $id, payload: _payload @ {atExpression} }},\n" +
               $"  *om_object{{ id: node_id, class_name: node_type, label: node_label }}{relFilter}\n" +
               ":sort relation_name, node_id";
    }

    private static async Task<IReadOnlyList<NeighborEntry>> NeighborRowsAsync(
        CozoOmRuntime runtime,
        string script,
        Dictionary<string, object?> parameters,
        CancellationToken cancellationToken)
    {
        var result = await runtime.Store.RunAsync(script, parameters, cancellationToken: cancellationToken);
        var output = new List<NeighborEntry>();
        foreach (var row in result.Rows)
        {
            output.Add(new NeighborEntry(
                await ClassLogic.ResolveRelationAsync(runtime, JsonRows.StringAt(row, 0) ?? "", cancellationToken),
                JsonRows.StringAt(row, 1) ?? "",
                await ClassLogic.ResolveClassAsync(runtime, JsonRows.StringAt(row, 2) ?? "", cancellationToken),
                JsonRows.StringAt(row, 3) ?? ""));
        }

        return output;
    }
}
