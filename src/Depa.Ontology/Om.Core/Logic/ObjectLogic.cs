using System.Text.Json;
using Depa.Cozo;
using Depa.Ontology.Contracts.Models;
using Depa.Ontology.Inputs;
using Depa.Ontology.Internals;
using Depa.Ontology.Runtime;
using Depa.Ontology.Support;

namespace Depa.Ontology.Logic;

internal static class ObjectLogic
{
    public static async Task CreateObjectAsync(CozoOmRuntime runtime, ObjectInput input, CancellationToken cancellationToken = default)
    {
        var entity = await NormalizeObjectAsync(runtime, input, cancellationToken);
        await runtime.Store.RunAsync(
            CozoScriptBuilder.InputInsert("om_object", ["id"], ["class_name", "label"]),
            LogicSupport.Params(("id", entity.Id), ("class_name", entity.ClassName), ("label", entity.Label)),
            cancellationToken: cancellationToken);
    }

    public static async Task UpsertObjectAsync(CozoOmRuntime runtime, ObjectInput input, CancellationToken cancellationToken = default)
    {
        var entity = await NormalizeObjectAsync(runtime, input, cancellationToken);
        await runtime.Store.RunAsync(
            CozoScriptBuilder.InputPut("om_object", ["id"], ["class_name", "label"]),
            LogicSupport.Params(("id", entity.Id), ("class_name", entity.ClassName), ("label", entity.Label)),
            cancellationToken: cancellationToken);
    }

    /// <summary>
    /// Physically deletes an object in a single transaction, cascading to every om_field_value row
    /// (all temporal versions, physical delete semantics) and every om_relation_link row where the object
    /// appears as source or target. Deleting a non-existent object is a harmless no-op.
    /// </summary>
    public static async Task DeleteObjectAsync(CozoOmRuntime runtime, string objectId, CancellationToken cancellationToken = default)
    {
        var id = OmConvert.RequireName(objectId, nameof(objectId));
        await using var tx = await runtime.Store.BeginTransactionAsync(write: true, cancellationToken);

        await tx.RunAsync(
            """
            ?[object_id, field_name, valid_time] :=
              *om_field_value{ object_id: $id, field_name, valid_time, value: _value, tx_time: _tx },
              object_id = $id
            :rm om_field_value {object_id, field_name, valid_time}
            """,
            LogicSupport.Params(("id", id)),
            cancellationToken: cancellationToken);
        await tx.RunAsync(
            """
            ?[from_object_id, relation_name, to_object_id, valid_time] :=
              from_object_id = $id,
              *om_relation_link{ from_object_id, relation_name, to_object_id, valid_time, payload: _payload, tx_time: _tx }
            ?[from_object_id, relation_name, to_object_id, valid_time] :=
              to_object_id = $id,
              *om_relation_link{ from_object_id, relation_name, to_object_id, valid_time, payload: _payload, tx_time: _tx }
            :rm om_relation_link {from_object_id, relation_name, to_object_id, valid_time}
            """,
            LogicSupport.Params(("id", id)),
            cancellationToken: cancellationToken);
        await tx.RunAsync(
            """
            ?[id] <- [[$id]]
            :rm om_object {id}
            """,
            LogicSupport.Params(("id", id)),
            cancellationToken: cancellationToken);

        await tx.CommitAsync(cancellationToken);
    }

    public static async Task<string> GetObjectClassAsync(CozoOmRuntime runtime, string objectId, CancellationToken cancellationToken = default)
    {
        var id = OmConvert.RequireName(objectId, nameof(objectId));
        var result = await runtime.Store.RunAsync(
            """
            ?[class_name] :=
              *om_object{ id: $id, class_name, label: _label }
            :limit 1
            """,
            LogicSupport.Params(("id", id)),
            cancellationToken: cancellationToken);
        if (result.Rows.Count == 0)
        {
            throw new CozoException($"Object '{id}' does not exist");
        }

        return await ClassLogic.ResolveClassAsync(runtime, JsonRows.StringAt(result.Rows[0], 0) ?? "", cancellationToken);
    }

    public static async Task SetFieldValueAsync(CozoOmRuntime runtime, SetFieldValueInput input, CancellationToken cancellationToken = default)
    {
        var id = OmConvert.RequireName(input.ObjectId, nameof(input.ObjectId));
        var className = await GetObjectClassAsync(runtime, id, cancellationToken);
        var fieldName = await ClassLogic.ResolveFieldAsync(runtime, className, input.FieldName, cancellationToken);
        await ValidateFieldValueTypeAsync(runtime, id, fieldName, input.Value, cancellationToken);
        var definitions = await ClassLogic.GetFieldDefinitionsAsync(runtime, className, cancellationToken);
        var definition = definitions[fieldName];

        if (input.Options?.SkipConstraints == true)
        {
            await WriteFieldValueAsync(runtime, id, fieldName, definition, input.Value, input.Options, cancellationToken);
            return;
        }

        await using var tx = await runtime.Store.BeginTransactionAsync(write: true, cancellationToken);
        var txRuntime = runtime with { Store = tx };
        await WriteFieldValueAsync(txRuntime, id, fieldName, definition, input.Value, input.Options, cancellationToken);
        var validation = await ConstraintLogic.ValidateObjectAsync(txRuntime, id, cancellationToken);
        if (!validation.Valid)
        {
            throw new CozoException(string.Join("; ", validation.Errors));
        }

        await tx.CommitAsync(cancellationToken);
    }

    private static async Task WriteFieldValueAsync(
        CozoOmRuntime runtime,
        string id,
        string fieldName,
        OmFieldDefinition definition,
        object? value,
        WriteOptions? options,
        CancellationToken cancellationToken)
    {
        var validTime = string.IsNullOrWhiteSpace(options?.ValidTime) ? "ASSERT" : options!.ValidTime!;
        var txTime = runtime.Options.TimeProvider.GetUtcNow().UtcDateTime.ToString("O");

        if (definition.ValueType == OmValueType.Validity)
        {
            var validity = OmConvert.NormalizeValidityInput(value, runtime.Options.TimeProvider.GetUtcNow());
            if (validity is null)
            {
                throw new CozoException($"Invalid Validity value for '{fieldName}'");
            }

            await runtime.Store.RunAsync(
                """
                input[object_id, field_name, valid_time, ts_us, is_assert, tx_time] <- [[$object_id, $field_name, $valid_time, $ts_us, $is_assert, $tx_time]]
                ?[object_id, field_name, valid_time, value, tx_time] :=
                  input[object_id, field_name, valid_time, ts_us, is_assert, tx_time],
                  value = validity(ts_us, is_assert)
                :put om_field_value {object_id, field_name, valid_time => value, tx_time}
                """,
                LogicSupport.Params(
                    ("object_id", id),
                    ("field_name", fieldName),
                    ("valid_time", validTime),
                    ("ts_us", validity.TimestampMicroseconds),
                    ("is_assert", validity.IsAssert),
                    ("tx_time", txTime)),
                cancellationToken: cancellationToken);
        }
        else
        {
            await runtime.Store.RunAsync(
                """
                ?[object_id, field_name, valid_time, value, tx_time] <- [[$object_id, $field_name, $valid_time, $value, $tx_time]]
                :put om_field_value {object_id, field_name, valid_time => value, tx_time}
                """,
                LogicSupport.Params(
                    ("object_id", id),
                    ("field_name", fieldName),
                    ("valid_time", validTime),
                    ("value", value),
                    ("tx_time", txTime)),
                cancellationToken: cancellationToken);
        }
    }

    public static async Task<JsonElement?> GetFieldValueAsync(CozoOmRuntime runtime, string objectId, string fieldName, CancellationToken cancellationToken = default)
    {
        return await GetFieldValueAtAsync(runtime, objectId, fieldName, "\"NOW\"", null, cancellationToken);
    }

    public static async Task<JsonElement?> GetFieldValueAsOfAsync(
        CozoOmRuntime runtime,
        string objectId,
        string fieldName,
        string asOf,
        CancellationToken cancellationToken = default)
    {
        var timestamp = OmConvert.NormalizeTimestamp(asOf, nameof(asOf));
        return await GetFieldValueAtAsync(runtime, objectId, fieldName, "$as_of", timestamp, cancellationToken);
    }

    internal static Task<JsonElement?> GetFieldValueAtNormalizedAsOfAsync(
        CozoOmRuntime runtime,
        string objectId,
        string fieldName,
        string normalizedAsOf,
        CancellationToken cancellationToken = default) =>
        GetFieldValueAtAsync(runtime, objectId, fieldName, "$as_of", normalizedAsOf, cancellationToken);

    public static async Task<IReadOnlyList<FieldValueHistoryRow>> GetFieldValueHistoryAsync(
        CozoOmRuntime runtime,
        string objectId,
        string fieldName,
        HistoryRangeOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var id = OmConvert.RequireName(objectId, nameof(objectId));
        var className = await GetObjectClassAsync(runtime, id, cancellationToken);
        var canonicalField = await ClassLogic.ResolveFieldAsync(runtime, className, fieldName, cancellationToken);
        var result = await runtime.Store.RunAsync(
            """
            ?[value, valid_time, tx_time] :=
              *om_field_value{ object_id: $object_id, field_name: $field_name, valid_time, value, tx_time }
            :sort valid_time
            """,
            LogicSupport.Params(("object_id", id), ("field_name", canonicalField)),
            cancellationToken: cancellationToken);

        return result.Rows
            .Select(row => new FieldValueHistoryRow(
                JsonRows.ElementAt(row, 0),
                JsonRows.StringAt(row, 1) ?? row[1].ToString(),
                JsonRows.StringAt(row, 2) ?? ""))
            .ToArray();
    }

    public static async Task ValidateFieldValueTypeAsync(CozoOmRuntime runtime, string objectId, string fieldName, object? value, CancellationToken cancellationToken = default)
    {
        var className = await GetObjectClassAsync(runtime, objectId, cancellationToken);
        var definitions = await ClassLogic.GetFieldDefinitionsAsync(runtime, className, cancellationToken);
        var canonicalField = await ClassLogic.ResolveFieldAsync(runtime, className, fieldName, cancellationToken);
        if (!definitions.TryGetValue(canonicalField, out var definition))
        {
            throw new CozoException($"Field '{canonicalField}' is not defined for class '{className}'");
        }

        if (definition.ValueType == OmValueType.Validity)
        {
            if (OmConvert.NormalizeValidityInput(value, runtime.Options.TimeProvider.GetUtcNow()) is null)
            {
                throw new CozoException($"Type mismatch: field '{canonicalField}' expects {definition.ValueType}, got {OmConvert.InferValueType(value)}");
            }

            return;
        }

        var actual = OmConvert.InferValueType(value);
        if (definition.ValueType != OmValueType.Json && definition.ValueType != actual)
        {
            throw new CozoException($"Type mismatch: field '{canonicalField}' expects {definition.ValueType}, got {actual}");
        }
    }

    public static async Task<IReadOnlyDictionary<string, JsonElement>> GetAllFieldValuesAsync(
        CozoOmRuntime runtime,
        string objectId,
        CancellationToken cancellationToken = default)
    {
        var id = OmConvert.RequireName(objectId, nameof(objectId));
        var className = await GetObjectClassAsync(runtime, id, cancellationToken);
        var result = await runtime.Store.RunAsync(
            """
            ?[field_name, value] :=
              *om_field_value{ object_id: $object_id, field_name, value @ "NOW" }
            :sort field_name
            """,
            LogicSupport.Params(("object_id", id)),
            cancellationToken: cancellationToken);
        var raw = result.Rows.ToDictionary(
            row => JsonRows.StringAt(row, 0) ?? "",
            row => JsonRows.ElementAt(row, 1),
            StringComparer.Ordinal);
        return await CanonicalizeStoredFieldValuesForClassAsync(runtime, className, raw, cancellationToken);
    }

    public static Task<ObjectViewRow?> GetObjectViewRowAsync(CozoOmRuntime runtime, string objectId, CancellationToken cancellationToken = default)
    {
        return GetObjectViewRowAtAsync(runtime, objectId, null, cancellationToken);
    }

    public static Task<ObjectViewRow?> GetObjectViewRowAsOfAsync(
        CozoOmRuntime runtime,
        string objectId,
        string asOf,
        CancellationToken cancellationToken = default)
    {
        return GetObjectViewRowAtAsync(runtime, objectId, OmConvert.NormalizeTimestamp(asOf, nameof(asOf)), cancellationToken);
    }

    private static async Task<ObjectViewRow?> GetObjectViewRowAtAsync(
        CozoOmRuntime runtime,
        string objectId,
        string? asOf,
        CancellationToken cancellationToken)
    {
        var id = OmConvert.RequireName(objectId, nameof(objectId));
        var result = await runtime.Store.RunAsync(
            """
            ?[class_name, label] :=
              *om_object{ id: $id, class_name, label }
            :limit 1
            """,
            LogicSupport.Params(("id", id)),
            cancellationToken: cancellationToken);
        if (result.Rows.Count == 0) return null;

        var className = await ClassLogic.ResolveClassAsync(runtime, JsonRows.StringAt(result.Rows[0], 0) ?? "", cancellationToken);
        var label = JsonRows.StringAt(result.Rows[0], 1) ?? "";
        var fieldValues = asOf is null
            ? await GetAllFieldValuesAsync(runtime, id, cancellationToken)
            : await GetAllFieldValuesAsOfAsync(runtime, id, className, asOf, cancellationToken);

        var computedCallbacks = await runtime.BehaviorGate.ResolveAsync(
            runtime,
            async (resolution, token) =>
            {
                var resolved = new List<(string ComputedPropName, Func<OmComputedPropContext, ValueTask<object?>> Compute)>();
                foreach (var (computedProp, ownerClass) in await ListEffectiveComputedPropsAsync(runtime, className, token))
                {
                    var compute = await ResolveComputedPropCallbackAsync(
                        runtime,
                        resolution,
                        ownerClass,
                        computedProp,
                        token);
                    if (!fieldValues.ContainsKey(computedProp) && compute is not null)
                    {
                        resolved.Add((computedProp, compute));
                    }
                }

                return resolved;
            },
            cancellationToken);

        foreach (var (computedProp, compute) in computedCallbacks)
        {
            var value = await compute(new OmComputedPropContext(runtime, id, className, asOf));
            fieldValues = fieldValues
                .Concat([new KeyValuePair<string, JsonElement>(computedProp, OmConvert.CloneToElement(value))])
                .ToDictionary(p => p.Key, p => p.Value, StringComparer.Ordinal);
        }

        var neighbors = asOf is null
            ? await RelationLogic.GetNeighborsAsync(runtime, id, direction: OmDirection.Outgoing, cancellationToken: cancellationToken)
            : await RelationLogic.GetNeighborsAsOfAsync(runtime, id, null, asOf, OmDirection.Outgoing, cancellationToken);
        var outgoing = neighbors.Outgoing
            .Select(n => new ObjectViewEdgeRow(n.RelationName, n.ObjectId, n.ClassName, n.Label))
            .ToArray();
        return new ObjectViewRow(id, className, label, fieldValues, outgoing);
    }

    public static async Task<IReadOnlyList<OmObjectRow>> FindByClassAsync(
        CozoOmRuntime runtime,
        string className,
        FindByClassCoreOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var canonical = await ClassLogic.ResolveClassAsync(runtime, className, cancellationToken);
        var types = options?.Exact == true
            ? [canonical]
            : new[] { canonical }.Concat(await ClassLogic.GetDescendantsAsync(runtime, canonical, cancellationToken)).ToArray();
        var result = await runtime.Store.RunAsync(
            """
            ?[id, class_name, label] :=
              *om_object{ id, class_name, label },
              is_in(class_name, $class_names)
            :sort id
            """,
            LogicSupport.Params(("class_names", types)),
            cancellationToken: cancellationToken);
        return result.Rows
            .Select(row => new OmObjectRow(
                JsonRows.StringAt(row, 0) ?? "",
                JsonRows.StringAt(row, 1) ?? "",
                JsonRows.StringAt(row, 2) ?? ""))
            .ToArray();
    }

    public static async Task<IReadOnlyList<FindByClassEntryRow>> FindByClassWithFieldValuesAsync(
        CozoOmRuntime runtime,
        string className,
        IReadOnlyDictionary<string, object?>? filter = null,
        FindByClassCoreOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var canonical = await ClassLogic.ResolveClassAsync(runtime, className, cancellationToken);
        var types = options?.Exact == true
            ? [canonical]
            : new[] { canonical }.Concat(await ClassLogic.GetDescendantsAsync(runtime, canonical, cancellationToken)).ToArray();
        var result = await runtime.Store.RunAsync(
            """
            ?[id, class_name, label] :=
              *om_object{ id, class_name, label },
              is_in(class_name, $class_names)
            :sort id
            """,
            LogicSupport.Params(("class_names", types)),
            cancellationToken: cancellationToken);

        var normalizedFilter = await NormalizeFindFilterAsync(runtime, canonical, filter, cancellationToken);
        var entries = new List<FindByClassEntryRow>();
        foreach (var row in result.Rows)
        {
            var id = JsonRows.StringAt(row, 0) ?? "";
            var storedClass = JsonRows.StringAt(row, 1) ?? "";
            var rowClass = await ClassLogic.ResolveClassAsync(runtime, storedClass, cancellationToken);
            var label = JsonRows.StringAt(row, 2) ?? "";
            var fieldValues = await GetAllFieldValuesAsync(runtime, id, cancellationToken);
            if (!FieldValuesMatch(fieldValues, normalizedFilter)) continue;
            entries.Add(new FindByClassEntryRow(id, rowClass, label, fieldValues));
        }

        return entries;
    }

    public static async Task<double> AggregateByClassAsync(
        CozoOmRuntime runtime,
        string className,
        string fieldName,
        string op,
        FindByClassCoreOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var normalizedOp = (op ?? "").Trim().ToLowerInvariant();
        if (normalizedOp is not ("sum" or "avg" or "min" or "max" or "count"))
        {
            throw new ArgumentException($"Unsupported aggregate op '{op}'", nameof(op));
        }

        var canonical = await ClassLogic.ResolveClassAsync(runtime, className, cancellationToken);
        var canonicalField = await ClassLogic.ResolveFieldAsync(runtime, canonical, fieldName, cancellationToken);
        var types = options?.Exact == true
            ? [canonical]
            : new[] { canonical }.Concat(await ClassLogic.GetDescendantsAsync(runtime, canonical, cancellationToken)).ToArray();
        var result = await runtime.Store.RunAsync(
            """
            ?[value] :=
              *om_object{ id, class_name, label: _label },
              is_in(class_name, $class_names),
              *om_field_value{ object_id: id, field_name: $field_name, value @ "NOW" }
            """,
            LogicSupport.Params(("class_names", types), ("field_name", canonicalField)),
            cancellationToken: cancellationToken);

        if (normalizedOp == "count")
        {
            return result.Rows.Count;
        }

        var values = result.Rows
            .Select(row => JsonRows.ElementAt(row, 0))
            .Where(element => element.ValueKind == JsonValueKind.Number && element.TryGetDouble(out _))
            .Select(element => element.GetDouble())
            .ToArray();
        if (values.Length == 0) return 0;
        return normalizedOp switch
        {
            "sum" => values.Sum(),
            "avg" => values.Average(),
            "min" => values.Min(),
            _ => values.Max(),
        };
    }

    private static async Task<OmObjectRow> NormalizeObjectAsync(CozoOmRuntime runtime, ObjectInput input, CancellationToken cancellationToken)
    {
        var id = OmConvert.RequireName(input.Id, nameof(input.Id));
        var className = await ClassLogic.ResolveClassAsync(runtime, input.ClassName, cancellationToken);
        if (!await ClassLogic.ClassExistsAsync(runtime, className, cancellationToken))
        {
            throw new CozoException($"Class '{className}' does not exist");
        }

        return new OmObjectRow(id, className, input.Label ?? "");
    }

    private static async Task<JsonElement?> GetFieldValueAtAsync(
        CozoOmRuntime runtime,
        string objectId,
        string fieldName,
        string atExpression,
        string? asOf,
        CancellationToken cancellationToken)
    {
        var id = OmConvert.RequireName(objectId, nameof(objectId));
        var className = await GetObjectClassAsync(runtime, id, cancellationToken);
        var canonicalField = await ClassLogic.ResolveFieldAsync(runtime, className, fieldName, cancellationToken);

        var parameters = LogicSupport.Params(("object_id", id), ("field_name", canonicalField));
        if (asOf is not null) parameters["as_of"] = asOf;

        var result = await runtime.Store.RunAsync(
            "?[value] :=\n" +
            $"  *om_field_value{{ object_id: $object_id, field_name: $field_name, value @ {atExpression} }}\n" +
            ":limit 1",
            parameters,
            cancellationToken: cancellationToken);
        JsonElement? storedValue = result.Rows.Count > 0
            ? JsonRows.ElementAt(result.Rows[0], 0)
            : null;

        if (storedValue is null)
        {
            foreach (var alias in await ClassLogic.GetFieldAliasesForCanonicalAsync(runtime, className, canonicalField, cancellationToken))
            {
                var aliasResult = await runtime.Store.RunAsync(
                    "?[value] :=\n" +
                    $"  *om_field_value{{ object_id: $object_id, field_name: $field_name, value @ {atExpression} }}\n" +
                    ":limit 1",
                    asOf is null
                        ? LogicSupport.Params(("object_id", id), ("field_name", alias))
                        : LogicSupport.Params(("object_id", id), ("field_name", alias), ("as_of", asOf)),
                    cancellationToken: cancellationToken);
                if (aliasResult.Rows.Count > 0)
                {
                    storedValue = JsonRows.ElementAt(aliasResult.Rows[0], 0);
                    break;
                }
            }
        }

        var compute = await runtime.BehaviorGate.ResolveAsync(
            runtime,
            async (resolution, token) =>
            {
                var computedPropOwner = await ResolveComputedPropOwnerAsync(runtime, className, canonicalField, token);
                return computedPropOwner is null
                    ? null
                    : await ResolveComputedPropCallbackAsync(runtime, resolution, computedPropOwner, canonicalField, token);
            },
            cancellationToken);
        if (storedValue is not null)
        {
            return storedValue;
        }

        if (compute is not null)
        {
            return OmConvert.CloneToElement(await compute(new OmComputedPropContext(runtime, id, className, asOf)));
        }

        return null;
    }

    private static async Task<IReadOnlyDictionary<string, JsonElement>> GetAllFieldValuesAsOfAsync(
        CozoOmRuntime runtime,
        string objectId,
        string className,
        string asOf,
        CancellationToken cancellationToken)
    {
        var result = await runtime.Store.RunAsync(
            """
            ?[field_name, value] :=
              *om_field_value{ object_id: $object_id, field_name, value @ $as_of }
            :sort field_name
            """,
            LogicSupport.Params(("object_id", objectId), ("as_of", asOf)),
            cancellationToken: cancellationToken);
        var raw = result.Rows.ToDictionary(
            row => JsonRows.StringAt(row, 0) ?? "",
            row => JsonRows.ElementAt(row, 1),
            StringComparer.Ordinal);
        return await CanonicalizeStoredFieldValuesForClassAsync(runtime, className, raw, cancellationToken);
    }

    private static async Task<IReadOnlyList<(string ComputedPropName, string OwnerClass)>> ListEffectiveComputedPropsAsync(
        CozoOmRuntime runtime,
        string className,
        CancellationToken cancellationToken)
    {
        var canonical = await ClassLogic.ResolveClassAsync(runtime, className, cancellationToken);
        var chain = (await ClassLogic.GetAncestorsAsync(runtime, canonical, cancellationToken)).Reverse().Concat([canonical]);
        var computedProps = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var currentClass in chain)
        {
            foreach (var computedPropName in await ConstraintLogic.ListComputedPropNamesAsync(runtime, currentClass, cancellationToken))
            {
                computedProps[computedPropName] = currentClass;
            }
        }

        return computedProps
            .OrderBy(item => item.Key, StringComparer.Ordinal)
            .Select(item => (item.Key, item.Value))
            .ToArray();
    }

    private static async Task<string?> ResolveComputedPropOwnerAsync(
        CozoOmRuntime runtime,
        string className,
        string fieldName,
        CancellationToken cancellationToken)
    {
        var canonical = await ClassLogic.ResolveClassAsync(runtime, className, cancellationToken);
        var canonicalField = await ClassLogic.ResolveFieldAsync(runtime, canonical, fieldName, cancellationToken);
        foreach (var (computedField, ownerClass) in await ListEffectiveComputedPropsAsync(runtime, canonical, cancellationToken))
        {
            var resolved = await ClassLogic.ResolveFieldAsync(runtime, canonical, computedField, cancellationToken);
            if (resolved == canonicalField)
            {
                return ownerClass;
            }
        }

        return null;
    }

    private static async Task<Func<OmComputedPropContext, ValueTask<object?>>?> ResolveComputedPropCallbackAsync(
        CozoOmRuntime runtime,
        BehaviorResolutionScope resolution,
        string ownerClass,
        string fieldName,
        CancellationToken cancellationToken)
    {
        var key = new BehaviorBindingKey(
            BehaviorKind.ComputedProp,
            ownerClass,
            fieldName,
            BehaviorCallbackSlot.Compute,
            BehaviorBindingLogic.NonInterceptorPhase,
            BehaviorBindingLogic.NonInterceptorSeq);
        await BehaviorReadinessLogic.EnsureReadyIfBoundAsync(runtime, resolution, key, cancellationToken);
        return resolution.RegistrySnapshot.ComputedProps.TryGetValue((ownerClass, fieldName), out var registration)
            ? registration.Callback
            : null;
    }

    private static async Task<IReadOnlyDictionary<string, JsonElement>> NormalizeFindFilterAsync(
        CozoOmRuntime runtime,
        string className,
        IReadOnlyDictionary<string, object?>? filter,
        CancellationToken cancellationToken)
    {
        var normalized = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        foreach (var (fieldName, expected) in filter ?? new Dictionary<string, object?>())
        {
            var canonicalField = await ClassLogic.ResolveFieldAsync(runtime, className, fieldName, cancellationToken);
            normalized[canonicalField] = OmConvert.CloneToElement(expected);
        }

        return normalized;
    }

    private static bool FieldValuesMatch(
        IReadOnlyDictionary<string, JsonElement> fieldValues,
        IReadOnlyDictionary<string, JsonElement> filter)
    {
        foreach (var (fieldName, expected) in filter)
        {
            if (!fieldValues.TryGetValue(fieldName, out var actual)) return false;
            if (!JsonElementEquals(actual, expected)) return false;
        }

        return true;
    }

    private static bool JsonElementEquals(JsonElement left, JsonElement right)
    {
        if (left.ValueKind != right.ValueKind)
        {
            if (left.ValueKind == JsonValueKind.Number && right.ValueKind == JsonValueKind.Number)
            {
                return left.TryGetDouble(out var leftDouble) &&
                       right.TryGetDouble(out var rightDouble) &&
                       leftDouble.Equals(rightDouble);
            }

            return false;
        }

        return left.ValueKind switch
        {
            JsonValueKind.String => left.GetString() == right.GetString(),
            JsonValueKind.Number => left.TryGetDouble(out var leftDouble) &&
                                    right.TryGetDouble(out var rightDouble) &&
                                    leftDouble.Equals(rightDouble),
            JsonValueKind.True or JsonValueKind.False => left.GetBoolean() == right.GetBoolean(),
            JsonValueKind.Null => true,
            _ => left.GetRawText() == right.GetRawText(),
        };
    }

    private static async Task<IReadOnlyDictionary<string, JsonElement>> CanonicalizeStoredFieldValuesForClassAsync(
        CozoOmRuntime runtime,
        string className,
        IReadOnlyDictionary<string, JsonElement> rawFieldValues,
        CancellationToken cancellationToken)
    {
        var fieldValues = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        var sourceIsCanonical = new Dictionary<string, bool>(StringComparer.Ordinal);
        foreach (var (storedField, value) in rawFieldValues)
        {
            if (string.IsNullOrWhiteSpace(storedField)) continue;
            var canonicalField = await ClassLogic.ResolveFieldAsync(runtime, className, storedField, cancellationToken);
            var isCanonicalSource = storedField == canonicalField;
            if (!fieldValues.ContainsKey(canonicalField))
            {
                fieldValues[canonicalField] = value;
                sourceIsCanonical[canonicalField] = isCanonicalSource;
                continue;
            }

            if (!sourceIsCanonical.GetValueOrDefault(canonicalField) && isCanonicalSource)
            {
                fieldValues[canonicalField] = value;
                sourceIsCanonical[canonicalField] = true;
            }
        }

        return fieldValues;
    }
}
