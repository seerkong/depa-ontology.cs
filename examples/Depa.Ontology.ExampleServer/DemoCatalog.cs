using System.Text.Json.Serialization;

namespace Depa.Ontology.ExampleServer;

public static class DemoCatalog
{
    public static IReadOnlyList<DemoDefinition> All { get; } =
    [
        Procurement(), Hr(), Crm(), ResourceGraph(), ApprovalFlow(), OrgTimeline()
    ];

    public static DemoDefinition? Find(string id) =>
        All.FirstOrDefault(demo => string.Equals(demo.Id, id, StringComparison.Ordinal));

    private static DemoDefinition Procurement() => Demo(
        "procurement", "采购管理",
        ["Supplier", "PurchaseOrder", "LineItem", "Warehouse", "Contract"],
        [A("Supplier", "rating", "Number"), A("PurchaseOrder", "total_amount", "Number"), A("LineItem", "unit_price", "Number"), A("Contract", "risk_score", "Number")],
        [R("placed_with", "PurchaseOrder", "Supplier"), R("has_line_item", "PurchaseOrder", "LineItem"), R("fulfilled_by", "LineItem", "Warehouse"), R("covered_by", "PurchaseOrder", "Contract"), R("supplies", "Supplier", "Warehouse")],
        [E("s:acme", "Supplier", "先达公司"), E("po:1001", "PurchaseOrder", "采购单-1001"), E("li:a", "LineItem", "钢梁 x200"), E("wh:east", "Warehouse", "华东仓储中心"), E("ct:master", "Contract", "主供应协议")],
        [P("s:acme", "rating", 4.5), P("po:1001", "total_amount", 54000), P("li:a", "unit_price", 270), P("ct:master", "risk_score", 15)],
        [L("po:1001", "placed_with", "s:acme"), L("po:1001", "has_line_item", "li:a"), L("li:a", "fulfilled_by", "wh:east"), L("po:1001", "covered_by", "ct:master"), L("s:acme", "supplies", "wh:east")],
        StandardPlans("LineItem", "unit_price", 20, "po:1001", ["has_line_item", "fulfilled_by", "placed_with", "covered_by"], "s:acme", ["supplies"], "Contract", "risk_score"));

    private static DemoDefinition Hr() => Demo(
        "hr", "人力资源",
        ["Employee", "Department", "Position", "Skill", "ReviewCycle"],
        [A("Employee", "salary", "Number"), A("Department", "budget", "Number")],
        [R("reports_to", "Employee", "Employee"), R("works_in", "Employee", "Department"), R("fills_position", "Employee", "Position"), R("requires_skill", "Position", "Skill")],
        [E("emp:alice", "Employee", "陈晓琳"), E("emp:bob", "Employee", "马志远"), E("dept:eng", "Department", "工程部"), E("pos:lead", "Position", "技术负责人"), E("sk:sys", "Skill", "系统设计")],
        [P("emp:alice", "salary", 180000), P("emp:bob", "salary", 145000), P("dept:eng", "budget", 1000000)],
        [L("emp:bob", "reports_to", "emp:alice"), L("emp:alice", "works_in", "dept:eng"), L("emp:alice", "fills_position", "pos:lead"), L("pos:lead", "requires_skill", "sk:sys")],
        StandardPlans("Employee", "salary", 140000, "emp:alice", ["reports_to"], "emp:alice", ["fills_position", "requires_skill"], "Employee", "salary")
            .Append(new DemoQueryPlan("employees", "table", "Employee", "salary")).ToArray());

    private static DemoDefinition Crm() => Demo(
        "crm", "CRM 销售",
        ["SalesRep", "Account", "Contact", "Lead", "Opportunity", "Activity"],
        [A("Opportunity", "amount", "Number"), A("Lead", "score", "Number")],
        [R("owned_by", "Opportunity", "SalesRep"), R("has_opportunity", "Account", "Opportunity"), R("has_contact", "Account", "Contact"), R("has_activity", "Opportunity", "Activity")],
        [E("rep:li", "SalesRep", "李雷"), E("acct:acme", "Account", "先达客户"), E("ct:alice", "Contact", "Alice"), E("opp:acme", "Opportunity", "ACME 续费")],
        [P("opp:acme", "amount", 120000)],
        [L("acct:acme", "has_opportunity", "opp:acme"), L("acct:acme", "has_contact", "ct:alice"), L("opp:acme", "owned_by", "rep:li")],
        StandardPlans("Opportunity", "amount", 60000, "acct:acme", ["has_opportunity", "owned_by", "has_contact"], "acct:acme", ["has_contact", "has_opportunity", "owned_by"], "Opportunity", "amount"));

    private static DemoDefinition ResourceGraph() => Demo(
        "resource-graph", "通用资源图（继承）",
        ["Resource", "ExecutableResource", "ApiService", "Worker", "Dataset"],
        [A("Resource", "resource_key", "String"), A("Dataset", "size_mb", "Number")],
        [R("depends_on", "ExecutableResource", "ExecutableResource"), R("contained_in", "Resource", "Resource"), R("produces", "ExecutableResource", "Dataset")],
        [E("scope:platform", "Resource", "Platform Scope"), E("api:gateway", "ApiService", "Gateway API"), E("api:catalog", "ApiService", "Catalog API"), E("data:catalog", "Dataset", "Catalog Dataset")],
        [P("api:gateway", "resource_key", "gateway-api"), P("api:catalog", "resource_key", "catalog-api"), P("data:catalog", "size_mb", 640)],
        [L("api:gateway", "depends_on", "api:catalog"), L("api:catalog", "produces", "data:catalog"), L("api:gateway", "contained_in", "scope:platform"), L("api:catalog", "contained_in", "scope:platform"), L("data:catalog", "contained_in", "scope:platform")],
        StandardPlans("Resource", "resource_key", null, "api:gateway", ["depends_on", "produces"], "scope:platform", ["contained_in"], "Dataset", "size_mb"),
        parents: new Dictionary<string, string> { ["ExecutableResource"] = "Resource", ["ApiService"] = "ExecutableResource", ["Worker"] = "ExecutableResource", ["Dataset"] = "Resource" });

    private static DemoDefinition ApprovalFlow() => Demo(
        "approval-flow", "审批流（Action + 约束 + 派生）",
        ["Department", "Approver", "ApprovalRequest"],
        [A("ApprovalRequest", "status", "String"), A("ApprovalRequest", "amount", "Number"), A("ApprovalRequest", "risk_score", "Number")],
        [R("belongs_to_dept", "ApprovalRequest", "Department"), R("assigned_to", "ApprovalRequest", "Approver")],
        [E("dept:finance", "Department", "财务部"), E("appr:alice", "Approver", "Alice"), E("req:1001", "ApprovalRequest", "采购申请 #1001")],
        [P("req:1001", "status", "draft"), P("req:1001", "amount", 25000), P("req:1001", "risk_score", 30)],
        [L("req:1001", "belongs_to_dept", "dept:finance"), L("req:1001", "assigned_to", "appr:alice")],
        [
            new("approveLowRisk", "action", "ApprovalRequest", "status", Action: "approve"),
            new("validateFinanceDept", "action", "ApprovalRequest", "risk_score", Action: "validate"),
            new("timelineAfterApprove", "action", "ApprovalRequest", "status", Action: "timeline"),
            new("requestsWithComputed", "table", "ApprovalRequest", "amount"),
            new("submitDraft", "action", "ApprovalRequest", "status", Action: "submit")
        ]);

    private static DemoDefinition OrgTimeline() => Demo(
        "org-timeline", "组织架构时间轴（Temporal）",
        ["Department", "Employee", "Team"],
        [A("Department", "cost_center", "String"), A("Employee", "title", "String")],
        [R("belongs_to", "Employee", "Department"), R("manages", "Employee", "Team")],
        [E("dept:eng", "Department", "Engineering"), E("emp:alice", "Employee", "Alice"), E("team:platform", "Team", "Platform Team")],
        [P("dept:eng", "cost_center", "ENG"), P("emp:alice", "title", "Engineering Manager")],
        [L("emp:alice", "belongs_to", "dept:eng"), L("emp:alice", "manages", "team:platform")],
        [
            new("snapshot_2024_06", "temporal", "Employee", Action: "snapshot", AsOf: "2024-06-01T00:00:00Z"),
            new("snapshot_2024_12", "temporal", "Employee", Action: "snapshot", AsOf: "2024-12-01T00:00:00Z"),
            new("dept_headcount_2024_12", "temporal", "Department", Action: "headcount", AsOf: "2024-12-01T00:00:00Z"),
            new("alice_timeline", "temporal", "Employee", Action: "timeline", AsOf: "2024-12-01T00:00:00Z")
        ]);

    private static DemoDefinition Demo(
        string id, string label, IReadOnlyList<string> types, IReadOnlyList<DemoAttribute> attributes,
        IReadOnlyList<DemoRelation> relations, IReadOnlyList<DemoEntity> entities, IReadOnlyList<DemoProperty> properties,
        IReadOnlyList<DemoEdge> edges, IReadOnlyList<DemoQueryPlan> plans, IReadOnlyDictionary<string, string>? parents = null)
    {
        var seed = new DemoSeed(types, attributes, relations, entities, properties, edges, parents ?? new Dictionary<string, string>());
        var queryById = plans.ToDictionary(plan => plan.Id, StringComparer.Ordinal);
        return new DemoDefinition(
            id, label, Tables(seed), plans.Select(plan => plan.ToQuery()).ToArray(), seed, queryById);
    }

    private static IReadOnlyList<DemoQueryPlan> StandardPlans(string type, string attr, double? min, string graphRoot, IReadOnlyList<string> graphRels, string treeRoot, IReadOnlyList<string> treeRels, string riskType, string riskAttr) =>
    [
        new("dslQuery", "table", type, attr, min),
        new("impactAnalysis", "impact", RootId: graphRoot, RelNames: graphRels),
        new("ownershipTree", "tree", RootId: treeRoot, RelNames: treeRels),
        new("riskHotspot", "risk", riskType, riskAttr)
    ];

    private static IReadOnlyList<DemoTable> Tables(DemoSeed seed) =>
    [
        new("类型定义", ["typeName", "parent_type", "mixins", "description"], seed.Types.Select(type => Row(("typeName", type), ("parent_type", seed.Parents.GetValueOrDefault(type, "")), ("mixins", ""), ("description", $"{type} 示例类型"))).ToArray()),
        new("属性定义", ["typeName", "attrName", "valueType", "required", "description"], seed.Attributes.Select(attr => Row(("typeName", attr.TypeName), ("attrName", attr.Name), ("valueType", attr.ValueType), ("required", false), ("description", ""))).ToArray()),
        new("关系定义", ["relName", "fromType", "toType", "directed", "description"], seed.Relations.Select(rel => Row(("relName", rel.Name), ("fromType", rel.FromType), ("toType", rel.ToType), ("directed", true), ("description", ""))).ToArray()),
        new("实体数据", ["id", "typeName", "label"], seed.Entities.Select(entity => Row(("id", entity.Id), ("typeName", entity.TypeName), ("label", entity.Label))).ToArray()),
        new("属性数据", ["entityId", "attrName", "value"], seed.Properties.Select(property => Row(("entityId", property.EntityId), ("attrName", property.Name), ("value", property.Value))).ToArray()),
        new("关系数据", ["fromId", "relName", "toId", "props"], seed.Edges.Select(edge => Row(("fromId", edge.FromId), ("relName", edge.Name), ("toId", edge.ToId), ("props", new Dictionary<string, object?>()))).ToArray())
    ];

    private static IReadOnlyDictionary<string, object?> Row(params (string Key, object? Value)[] values) =>
        values.ToDictionary(value => value.Key, value => value.Value, StringComparer.Ordinal);

    private static DemoAttribute A(string typeName, string name, string valueType) => new(typeName, name, valueType);
    private static DemoRelation R(string name, string fromType, string toType) => new(name, fromType, toType);
    private static DemoEntity E(string id, string typeName, string label) => new(id, typeName, label);
    private static DemoProperty P(string entityId, string name, object? value) => new(entityId, name, value);
    private static DemoEdge L(string fromId, string name, string toId) => new(fromId, name, toId);
}

public sealed record DemoDefinition(string Id, string Label, IReadOnlyList<DemoTable> Tables, IReadOnlyList<DemoQuery> Queries)
{
    [JsonIgnore] public DemoSeed Seed { get; init; } = default!;
    [JsonIgnore] public IReadOnlyDictionary<string, DemoQueryPlan> Plans { get; init; } = default!;

    public DemoDefinition(string id, string label, IReadOnlyList<DemoTable> tables, IReadOnlyList<DemoQuery> queries, DemoSeed seed, IReadOnlyDictionary<string, DemoQueryPlan> plans)
        : this(id, label, tables, queries) => (Seed, Plans) = (seed, plans);
}

public sealed record DemoTable(string Name, IReadOnlyList<string> Columns, IReadOnlyList<IReadOnlyDictionary<string, object?>> Rows);
public sealed record DemoQuery(string Id, string Label, string Description, string Meaning, string Dsl, string DefaultView);
public sealed record DemoSeed(IReadOnlyList<string> Types, IReadOnlyList<DemoAttribute> Attributes, IReadOnlyList<DemoRelation> Relations, IReadOnlyList<DemoEntity> Entities, IReadOnlyList<DemoProperty> Properties, IReadOnlyList<DemoEdge> Edges, IReadOnlyDictionary<string, string> Parents);
public sealed record DemoAttribute(string TypeName, string Name, string ValueType);
public sealed record DemoRelation(string Name, string FromType, string ToType);
public sealed record DemoEntity(string Id, string TypeName, string Label);
public sealed record DemoProperty(string EntityId, string Name, object? Value);
public sealed record DemoEdge(string FromId, string Name, string ToId);
public sealed record DemoQueryPlan(string Id, string Kind, string? TypeName = null, string? AttrName = null, double? MinNumber = null, string? RootId = null, IReadOnlyList<string>? RelNames = null, string? Action = null, string? AsOf = null)
{
    public DemoQuery ToQuery() => new(Id, Id switch
    {
        "dslQuery" => "DSL 查询", "impactAnalysis" => "影响分析", "ownershipTree" => "所有权树", "riskHotspot" => "风险热点", _ => Id
    }, $"{Id} 示例查询", Id, string.Empty, Kind switch { "impact" => "graph", "tree" => "tree", _ => "table" });
}

public sealed record RunRequest(string DemoId, string QueryId, IReadOnlyList<DemoTable>? Tables = null);
public sealed record TableResult(IReadOnlyList<string> Columns, IReadOnlyList<IReadOnlyDictionary<string, object?>> Rows);
public sealed record FrontendGraph(IReadOnlyList<FrontendGraphNode> Nodes, IReadOnlyList<FrontendGraphEdge> Edges);
public sealed record FrontendGraphNode(string Id, string Label, string? Group = null);
public sealed record FrontendGraphEdge(string From, string To, string? Label = null);
public sealed record FrontendTreeNode(string Id, string Label, IReadOnlyList<FrontendTreeNode>? Children = null);
