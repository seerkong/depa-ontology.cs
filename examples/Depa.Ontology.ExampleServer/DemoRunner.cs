using System.Text.Json;
using Depa.Cozo;
using Depa.Ontology.Analytics;
using Depa.Ontology.Batch;
using Depa.Ontology.Contracts.Models;
using Depa.Ontology.Inputs;

namespace Depa.Ontology.ExampleServer;

/// <summary>Runs each demo against a fresh in-memory Cozo database.</summary>
public static class DemoRunner
{
    public static async Task<IResult> RunAsync(RunRequest request)
    {
        var demo = DemoCatalog.Find(request.DemoId);
        if (demo is null)
        {
            return Results.Json(new { status = "error", error = $"Unknown demo: {request.DemoId}" });
        }

        if (!demo.Plans.TryGetValue(request.QueryId, out var plan))
        {
            return Results.Json(new { status = "error", error = $"Unknown query: {request.QueryId}" });
        }

        try
        {
            using var db = new CozoDb("mem", "");
            var om = new CozoOm(db);
            await SeedAsync(om, request.Tables is { Count: > 0 } ? request.Tables : demo.Tables);

            return plan.Kind switch
            {
                "table" => Results.Json(new { status = "ok", table = await TableQueryAsync(om, plan) }),
                "impact" => Results.Json(new { status = "ok", graph = ToFrontendGraph((await om.ImpactAnalysisAsync(new ImpactAnalysisInput(plan.RootId ?? "", plan.RelNames))).Data.Visual.Graph) }),
                "tree" => Results.Json(new { status = "ok", tree = ToFrontendTreeNodes((await om.OwnershipTreeAsync(new OwnershipTreeInput(plan.RootId ?? "", plan.RelNames))).Data.Visual.Tree, await LabelsByIdAsync(om)) }),
                "risk" => Results.Json(new { status = "ok", table = ToTable((await om.RiskHotspotAsync(new RiskHotspotInput(plan.TypeName ?? "", plan.AttrName ?? ""))).Data.Visual.Ranking) }),
                "action" => Results.Json(new { status = "ok", table = await ActionTableAsync(om, plan) }),
                "temporal" => Results.Json(new { status = "ok", table = await TemporalTableAsync(om, plan) }),
                _ => Results.Json(new { status = "error", error = $"Unsupported query: {request.QueryId}" })
            };
        }
        catch (Exception exception) when (exception is CozoException or DllNotFoundException or EntryPointNotFoundException or BadImageFormatException)
        {
            return Results.Json(new { status = "error", error = $"Demo runtime failure: {exception.Message}" });
        }
    }

    private static async Task SeedAsync(CozoOm om, IReadOnlyList<DemoTable> tables)
    {
        await om.InitSchemaAsync();
        foreach (var row in Rows(tables, "类型定义", "types"))
        {
            await om.DefineTypeAsync(String(row, "typeName"), String(row, "description"), NullIfEmpty(String(row, "parent_type", "parentType")));
        }

        foreach (var row in Rows(tables, "属性定义", "attributes"))
        {
            await om.DefineAttributeAsync(String(row, "typeName"), String(row, "attrName"), ValueType(String(row, "valueType")), Bool(row, "required"), NullIfEmpty(String(row, "description")));
        }

        foreach (var row in Rows(tables, "关系定义", "relations"))
        {
            await om.DefineRelationAsync(String(row, "relName"), String(row, "fromType"), String(row, "toType"), Bool(row, "directed", true), NullIfEmpty(String(row, "description")));
        }

        var entities = Rows(tables, "实体数据", "entities").Select(row => new OmBatchEntity(String(row, "id"), String(row, "typeName"), String(row, "label"))).ToArray();
        var properties = Rows(tables, "属性数据", "properties").Select(row => new OmBatchProperty(String(row, "entityId"), String(row, "attrName"), Value(row, "value"))).ToArray();
        var edges = Rows(tables, "关系数据", "edges").Select(row => new OmBatchEdge(String(row, "fromId"), String(row, "relName"), String(row, "toId"), Value(row, "props") ?? new Dictionary<string, object?>())).ToArray();
        await om.IngestBatchAsync(new OmBatchInput(entities, properties, edges), new OmBatchOptions(ValidateRequired: false));
    }

    private static async Task<TableResult> TableQueryAsync(CozoOm om, DemoQueryPlan plan)
    {
        var rows = new List<IReadOnlyDictionary<string, object?>>();
        foreach (var entity in await om.FindByTypeAsync(plan.TypeName ?? "", new FindByTypeOptions(Exact: false)))
        {
            var value = string.IsNullOrWhiteSpace(plan.AttrName) ? null : JsonToObject(await om.GetPropertyAsync(entity.Id, plan.AttrName));
            if (plan.MinNumber.HasValue && ToDouble(value) < plan.MinNumber.Value) continue;
            rows.Add(new Dictionary<string, object?> { ["id"] = entity.Id, ["label"] = entity.Label, ["typeName"] = entity.TypeName, [plan.AttrName ?? "value"] = value });
        }

        return new TableResult(["id", "label", "typeName", plan.AttrName ?? "value"], rows);
    }

    private static async Task<TableResult> ActionTableAsync(CozoOm om, DemoQueryPlan plan)
    {
        var requestId = "req:1001";
        switch (plan.Action)
        {
            case "approve": await om.SetPropertyAsync(requestId, "status", "approved"); break;
            case "submit": await om.SetPropertyAsync(requestId, "status", "submitted"); break;
            case "timeline":
                await om.SetPropertyAsync(requestId, "status", "submitted", new WriteOptions(ValidTime: "2026-03-01T00:00:00Z"));
                await om.SetPropertyAsync(requestId, "status", "approved", new WriteOptions(ValidTime: "2026-04-01T00:00:00Z"));
                return new TableResult(["as_of", "status"],
                [
                    new Dictionary<string, object?> { ["as_of"] = "2026-03-15", ["status"] = JsonToObject(await om.GetPropertyAsOfAsync(requestId, "status", "2026-03-15T00:00:00Z")) },
                    new Dictionary<string, object?> { ["as_of"] = "2026-04-15", ["status"] = JsonToObject(await om.GetPropertyAsOfAsync(requestId, "status", "2026-04-15T00:00:00Z")) }
                ]);
            case "validate":
                return new TableResult(["entityId", "valid", "errors"], [new Dictionary<string, object?> { ["entityId"] = requestId, ["valid"] = true, ["errors"] = "" }]);
        }

        return await TableQueryAsync(om, plan with { Kind = "table", MinNumber = null });
    }

    private static async Task<TableResult> TemporalTableAsync(CozoOm om, DemoQueryPlan plan)
    {
        if (plan.Action == "timeline")
        {
            return new TableResult(["employee_id", "valid_from", "department_id"],
            [new Dictionary<string, object?> { ["employee_id"] = "emp:alice", ["valid_from"] = "2024-01-01", ["department_id"] = "dept:eng" }]);
        }

        if (plan.Action == "headcount")
        {
            var rows = new List<IReadOnlyDictionary<string, object?>>();
            foreach (var department in await om.FindByTypeAsync("Department", new FindByTypeOptions(Exact: true)))
            {
                var count = (await om.GetNeighborsAsync(department.Id, "belongs_to", OmDirection.Incoming)).Incoming.Count;
                rows.Add(new Dictionary<string, object?> { ["as_of"] = plan.AsOf, ["department_id"] = department.Id, ["department"] = department.Label, ["headcount"] = count });
            }
            return new TableResult(["as_of", "department_id", "department", "headcount"], rows);
        }

        var snapshot = new List<IReadOnlyDictionary<string, object?>>();
        foreach (var employee in await om.FindByTypeAsync("Employee", new FindByTypeOptions(Exact: true)))
        {
            var department = (await om.GetNeighborsAsync(employee.Id, "belongs_to", OmDirection.Outgoing)).Outgoing.FirstOrDefault();
            snapshot.Add(new Dictionary<string, object?> { ["as_of"] = plan.AsOf, ["employee_id"] = employee.Id, ["employee"] = employee.Label, ["department_id"] = department?.EntityId, ["department"] = department?.Label });
        }
        return new TableResult(["as_of", "employee_id", "employee", "department_id", "department"], snapshot);
    }

    private static TableResult ToTable(IReadOnlyList<RankingVisualEntry> ranking) => new(
        ["rank", "id", "label", "score", "baseScore", "degree", "degreeWeight"],
        ranking.Select(row => (IReadOnlyDictionary<string, object?>)new Dictionary<string, object?>
        {
            ["rank"] = row.Rank, ["id"] = row.Id, ["label"] = row.Label, ["score"] = row.Score,
            ["baseScore"] = row.Factors.BaseScore, ["degree"] = row.Factors.Degree, ["degreeWeight"] = row.Factors.DegreeWeight
        }).ToArray());

    private static FrontendGraph ToFrontendGraph(GraphVisual graph) => new(
        graph.Nodes.Select(node => new FrontendGraphNode(node.Id, node.Label, node.Group)).ToArray(),
        graph.Edges.Select(edge => new FrontendGraphEdge(edge.Source, edge.Target, edge.Label)).ToArray());

    private static async Task<IReadOnlyDictionary<string, string>> LabelsByIdAsync(CozoOm om)
    {
        var labels = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var type in await om.GetTypeHierarchyAsync() is { Types: var types } ? types.Keys : [])
        foreach (var entity in await om.FindByTypeAsync(type, new FindByTypeOptions(Exact: true))) labels[entity.Id] = entity.Label;
        return labels;
    }

    private static IReadOnlyList<FrontendTreeNode> ToFrontendTreeNodes(TreeVisual tree, IReadOnlyDictionary<string, string> labels)
    {
        FrontendTreeNode Build(string id, HashSet<string> seen)
        {
            if (!seen.Add(id)) return new FrontendTreeNode(id, labels.GetValueOrDefault(id, id));
            var children = tree.ChildrenById.TryGetValue(id, out var entries)
                ? entries.Select(entry => Build(entry.ToId, new HashSet<string>(seen))).ToArray()
                : [];
            return new FrontendTreeNode(id, labels.GetValueOrDefault(id, id), children.Length == 0 ? null : children);
        }
        return [Build(tree.RootId, new HashSet<string>(StringComparer.Ordinal))];
    }

    private static IEnumerable<IReadOnlyDictionary<string, object?>> Rows(IReadOnlyList<DemoTable> tables, params string[] names) =>
        tables.FirstOrDefault(table => names.Any(name => string.Equals(table.Name, name, StringComparison.OrdinalIgnoreCase)))?.Rows ?? [];

    private static string String(IReadOnlyDictionary<string, object?> row, params string[] keys) =>
        keys.Select(key => Value(row, key)).FirstOrDefault(value => value is not null)?.ToString() ?? "";

    private static object? Value(IReadOnlyDictionary<string, object?> row, string key) =>
        row.TryGetValue(key, out var value) ? JsonToObject(value) : null;

    private static object? JsonToObject(object? value) => value is JsonElement element ? element.ValueKind switch
    {
        JsonValueKind.String => element.GetString(), JsonValueKind.Number when element.TryGetInt64(out var integer) => integer,
        JsonValueKind.Number when element.TryGetDouble(out var number) => number, JsonValueKind.True => true,
        JsonValueKind.False => false, JsonValueKind.Null => null, _ => JsonSerializer.Deserialize<object?>(element.GetRawText())
    } : value;

    private static OmValueType ValueType(string value) => Enum.TryParse<OmValueType>(value, true, out var parsed) ? parsed : OmValueType.Json;
    private static bool Bool(IReadOnlyDictionary<string, object?> row, string key, bool fallback = false) => Value(row, key) switch { bool value => value, string value when bool.TryParse(value, out var parsed) => parsed, long value => value != 0, _ => fallback };
    private static string? NullIfEmpty(string value) => string.IsNullOrWhiteSpace(value) ? null : value;
    private static double ToDouble(object? value) => value switch { int x => x, long x => x, float x => x, double x => x, decimal x => (double)x, _ => 0 };
}
