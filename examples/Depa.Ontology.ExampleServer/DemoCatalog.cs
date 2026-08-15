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
        [F("Supplier", "rating", "Number"), F("PurchaseOrder", "total_amount", "Number"), F("LineItem", "unit_price", "Number"), F("Contract", "risk_score", "Number")],
        [R("placed_with", "PurchaseOrder", "Supplier"), R("has_line_item", "PurchaseOrder", "LineItem"), R("fulfilled_by", "LineItem", "Warehouse"), R("covered_by", "PurchaseOrder", "Contract"), R("supplies", "Supplier", "Warehouse")],
        [E("s:acme", "Supplier", "先达公司"), E("po:1001", "PurchaseOrder", "采购单-1001"), E("li:a", "LineItem", "钢梁 x200"), E("wh:east", "Warehouse", "华东仓储中心"), E("ct:master", "Contract", "主供应协议")],
        [FV("s:acme", "rating", 4.5), FV("po:1001", "total_amount", 54000), FV("li:a", "unit_price", 270), FV("ct:master", "risk_score", 15)],
        [L("po:1001", "placed_with", "s:acme"), L("po:1001", "has_line_item", "li:a"), L("li:a", "fulfilled_by", "wh:east"), L("po:1001", "covered_by", "ct:master"), L("s:acme", "supplies", "wh:east")],
        StandardPlans("LineItem", "unit_price", 20, "po:1001", ["has_line_item", "fulfilled_by", "placed_with", "covered_by"], "s:acme", ["supplies"], "Contract", "risk_score"));

    private static DemoDefinition Hr() => Demo(
        "hr", "人力资源",
        ["Employee", "Department", "Position", "Skill", "ReviewCycle"],
        [F("Employee", "salary", "Number"), F("Department", "budget", "Number")],
        [R("reports_to", "Employee", "Employee"), R("works_in", "Employee", "Department"), R("fills_position", "Employee", "Position"), R("requires_skill", "Position", "Skill")],
        [E("emp:alice", "Employee", "陈晓琳"), E("emp:bob", "Employee", "马志远"), E("dept:eng", "Department", "工程部"), E("pos:lead", "Position", "技术负责人"), E("sk:sys", "Skill", "系统设计")],
        [FV("emp:alice", "salary", 180000), FV("emp:bob", "salary", 145000), FV("dept:eng", "budget", 1000000)],
        [L("emp:bob", "reports_to", "emp:alice"), L("emp:alice", "works_in", "dept:eng"), L("emp:alice", "fills_position", "pos:lead"), L("pos:lead", "requires_skill", "sk:sys")],
        StandardPlans("Employee", "salary", 140000, "emp:alice", ["reports_to"], "emp:alice", ["fills_position", "requires_skill"], "Employee", "salary")
            .Append(new DemoQueryPlan("employees", "table", "Employee", "salary")).ToArray());

    private static DemoDefinition Crm() => Demo(
        "crm", "CRM 销售",
        ["SalesRep", "Account", "Contact", "Lead", "Opportunity", "Activity"],
        [F("Opportunity", "amount", "Number"), F("Lead", "score", "Number")],
        [R("owned_by", "Opportunity", "SalesRep"), R("has_opportunity", "Account", "Opportunity"), R("has_contact", "Account", "Contact"), R("has_activity", "Opportunity", "Activity")],
        [E("rep:li", "SalesRep", "李雷"), E("acct:acme", "Account", "先达客户"), E("ct:alice", "Contact", "Alice"), E("opp:acme", "Opportunity", "ACME 续费")],
        [FV("opp:acme", "amount", 120000)],
        [L("acct:acme", "has_opportunity", "opp:acme"), L("acct:acme", "has_contact", "ct:alice"), L("opp:acme", "owned_by", "rep:li")],
        StandardPlans("Opportunity", "amount", 60000, "acct:acme", ["has_opportunity", "owned_by", "has_contact"], "acct:acme", ["has_contact", "has_opportunity", "owned_by"], "Opportunity", "amount"));

    private static DemoDefinition ResourceGraph() => Demo(
        "resource-graph", "通用资源图（继承）",
        ["Resource", "ExecutableResource", "ApiService", "Worker", "Dataset"],
        [F("Resource", "resource_key", "String"), F("Dataset", "size_mb", "Number")],
        [R("depends_on", "ExecutableResource", "ExecutableResource"), R("contained_in", "Resource", "Resource"), R("produces", "ExecutableResource", "Dataset")],
        [E("scope:platform", "Resource", "Platform Scope"), E("api:gateway", "ApiService", "Gateway API"), E("api:catalog", "ApiService", "Catalog API"), E("data:catalog", "Dataset", "Catalog Dataset")],
        [FV("api:gateway", "resource_key", "gateway-api"), FV("api:catalog", "resource_key", "catalog-api"), FV("data:catalog", "size_mb", 640)],
        [L("api:gateway", "depends_on", "api:catalog"), L("api:catalog", "produces", "data:catalog"), L("api:gateway", "contained_in", "scope:platform"), L("api:catalog", "contained_in", "scope:platform"), L("data:catalog", "contained_in", "scope:platform")],
        StandardPlans("Resource", "resource_key", null, "api:gateway", ["depends_on", "produces"], "scope:platform", ["contained_in"], "Dataset", "size_mb"),
        parents: new Dictionary<string, string> { ["ExecutableResource"] = "Resource", ["ApiService"] = "ExecutableResource", ["Worker"] = "ExecutableResource", ["Dataset"] = "Resource" });

    private static DemoDefinition ApprovalFlow() => Demo(
        "approval-flow", "审批流（Operation + 约束 + 派生）",
        ["Department", "Approver", "ApprovalRequest"],
        [F("ApprovalRequest", "status", "String"), F("ApprovalRequest", "amount", "Number"), F("ApprovalRequest", "risk_score", "Number")],
        [R("belongs_to_dept", "ApprovalRequest", "Department"), R("assigned_to", "ApprovalRequest", "Approver")],
        [E("dept:finance", "Department", "财务部"), E("appr:alice", "Approver", "Alice"), E("req:1001", "ApprovalRequest", "采购申请 #1001")],
        [FV("req:1001", "status", "draft"), FV("req:1001", "amount", 25000), FV("req:1001", "risk_score", 30)],
        [L("req:1001", "belongs_to_dept", "dept:finance"), L("req:1001", "assigned_to", "appr:alice")],
        [
            new("approveLowRisk", "operation", "ApprovalRequest", "status", Operation: "approve"),
            new("validateFinanceDept", "operation", "ApprovalRequest", "risk_score", Operation: "validate"),
            new("timelineAfterApprove", "operation", "ApprovalRequest", "status", Operation: "timeline"),
            new("requestsWithComputed", "table", "ApprovalRequest", "amount"),
            new("submitDraft", "operation", "ApprovalRequest", "status", Operation: "submit")
        ],
        computedProps: [CP("ApprovalRequest", "approval_band", "ComputedProp derived from amount and risk score")],
        operations: [O("ApprovalRequest", "approve", "Approve a low-risk request"), O("ApprovalRequest", "validate", "Validate request readiness"), O("ApprovalRequest", "submit", "Submit draft request"), O("ApprovalRequest", "timeline", "Show approval timeline")]);

    private static DemoDefinition OrgTimeline() => Demo(
        "org-timeline", "组织架构时间轴（Temporal）",
        ["Department", "Employee", "Team"],
        [F("Department", "cost_center", "String"), F("Employee", "title", "String")],
        [R("belongs_to", "Employee", "Department"), R("manages", "Employee", "Team")],
        [E("dept:eng", "Department", "Engineering"), E("emp:alice", "Employee", "Alice"), E("team:platform", "Team", "Platform Team")],
        [FV("dept:eng", "cost_center", "ENG"), FV("emp:alice", "title", "Engineering Manager")],
        [L("emp:alice", "belongs_to", "dept:eng"), L("emp:alice", "manages", "team:platform")],
        [
            new("snapshot_2024_06", "temporal", "Employee", Operation: "snapshot", AsOf: "2024-06-01T00:00:00Z"),
            new("snapshot_2024_12", "temporal", "Employee", Operation: "snapshot", AsOf: "2024-12-01T00:00:00Z"),
            new("dept_headcount_2024_12", "temporal", "Department", Operation: "headcount", AsOf: "2024-12-01T00:00:00Z"),
            new("alice_timeline", "temporal", "Employee", Operation: "timeline", AsOf: "2024-12-01T00:00:00Z")
        ]);

    private static DemoDefinition Demo(
        string id, string label, IReadOnlyList<string> classes, IReadOnlyList<DemoField> fields,
        IReadOnlyList<DemoRelation> relations, IReadOnlyList<DemoObject> objects, IReadOnlyList<DemoFieldValue> fieldValues,
        IReadOnlyList<DemoRelationLink> relationLinks, IReadOnlyList<DemoQueryPlan> plans, IReadOnlyDictionary<string, string>? parents = null,
        IReadOnlyList<DemoComputedProp>? computedProps = null, IReadOnlyList<DemoOperation>? operations = null)
    {
        var seed = new DemoSeed(classes, fields, relations, objects, fieldValues, relationLinks, computedProps ?? [], operations ?? [], parents ?? new Dictionary<string, string>());
        var queryById = plans.ToDictionary(plan => plan.Id, StringComparer.Ordinal);
        return new DemoDefinition(
            id, label, Tables(seed), plans.Select(plan => plan.ToQuery()).ToArray(), seed, queryById);
    }

    private static IReadOnlyList<DemoQueryPlan> StandardPlans(string className, string fieldName, double? min, string graphRoot, IReadOnlyList<string> graphRels, string treeRoot, IReadOnlyList<string> treeRels, string riskClass, string riskField) =>
    [
        new("dslQuery", "table", className, fieldName, min),
        new("impactAnalysis", "impact", RootId: graphRoot, RelationNames: graphRels),
        new("ownershipTree", "tree", RootId: treeRoot, RelationNames: treeRels),
        new("riskHotspot", "risk", riskClass, riskField)
    ];

    private static IReadOnlyList<DemoTable> Tables(DemoSeed seed) =>
    [
        new("Class 定义", ["className", "parentClass", "mixins", "description"], seed.Classes.Select(className => Row(("className", className), ("parentClass", seed.Parents.GetValueOrDefault(className, "")), ("mixins", ""), ("description", $"{className} 示例 Class"))).ToArray()),
        new("Field 定义", ["className", "fieldName", "valueKind", "required", "description"], seed.Fields.Select(field => Row(("className", field.ClassName), ("fieldName", field.Name), ("valueKind", field.ValueKind), ("required", false), ("description", ""))).ToArray()),
        new("RelationDef 定义", ["relationName", "fromClass", "toClass", "directed", "description"], seed.Relations.Select(rel => Row(("relationName", rel.Name), ("fromClass", rel.FromClass), ("toClass", rel.ToClass), ("directed", true), ("description", ""))).ToArray()),
        new("ComputedProp 定义", ["className", "computedPropName", "description"], seed.ComputedProps.Select(computedProp => Row(("className", computedProp.ClassName), ("computedPropName", computedProp.Name), ("description", computedProp.Description))).ToArray()),
        new("Operation 定义", ["className", "operationName", "description"], seed.Operations.Select(operation => Row(("className", operation.ClassName), ("operationName", operation.Name), ("description", operation.Description))).ToArray()),
        new("Object 数据", ["id", "className", "label"], seed.Objects.Select(obj => Row(("id", obj.Id), ("className", obj.ClassName), ("label", obj.Label))).ToArray()),
        new("FieldValue 数据", ["objectId", "fieldName", "value"], seed.FieldValues.Select(fieldValue => Row(("objectId", fieldValue.ObjectId), ("fieldName", fieldValue.Name), ("value", fieldValue.Value))).ToArray()),
        new("RelationLink 数据", ["fromObjectId", "relationName", "toObjectId", "payload"], seed.RelationLinks.Select(link => Row(("fromObjectId", link.FromObjectId), ("relationName", link.Name), ("toObjectId", link.ToObjectId), ("payload", new Dictionary<string, object?>()))).ToArray())
    ];

    private static IReadOnlyDictionary<string, object?> Row(params (string Key, object? Value)[] values) =>
        values.ToDictionary(value => value.Key, value => value.Value, StringComparer.Ordinal);

    private static DemoField F(string className, string name, string valueKind) => new(className, name, valueKind);
    private static DemoRelation R(string name, string fromClass, string toClass) => new(name, fromClass, toClass);
    private static DemoObject E(string id, string className, string label) => new(id, className, label);
    private static DemoFieldValue FV(string objectId, string name, object? value) => new(objectId, name, value);
    private static DemoRelationLink L(string fromObjectId, string name, string toObjectId) => new(fromObjectId, name, toObjectId);
    private static DemoComputedProp CP(string className, string name, string description) => new(className, name, description);
    private static DemoOperation O(string className, string name, string description) => new(className, name, description);
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
public sealed record DemoSeed(IReadOnlyList<string> Classes, IReadOnlyList<DemoField> Fields, IReadOnlyList<DemoRelation> Relations, IReadOnlyList<DemoObject> Objects, IReadOnlyList<DemoFieldValue> FieldValues, IReadOnlyList<DemoRelationLink> RelationLinks, IReadOnlyList<DemoComputedProp> ComputedProps, IReadOnlyList<DemoOperation> Operations, IReadOnlyDictionary<string, string> Parents);
public sealed record DemoField(string ClassName, string Name, string ValueKind);
public sealed record DemoRelation(string Name, string FromClass, string ToClass);
public sealed record DemoObject(string Id, string ClassName, string Label);
public sealed record DemoFieldValue(string ObjectId, string Name, object? Value);
public sealed record DemoRelationLink(string FromObjectId, string Name, string ToObjectId);
public sealed record DemoComputedProp(string ClassName, string Name, string Description);
public sealed record DemoOperation(string ClassName, string Name, string Description);
public sealed record DemoQueryPlan(string Id, string Kind, string? ClassName = null, string? FieldName = null, double? MinNumber = null, string? RootId = null, IReadOnlyList<string>? RelationNames = null, string? Operation = null, string? AsOf = null)
{
    public DemoQuery ToQuery() => new(Id, Id switch
    {
        "dslQuery" => "DSL 查询", "impactAnalysis" => "影响分析", "ownershipTree" => "所有权树", "riskHotspot" => "风险热点", _ => Id
    }, $"{Id} 示例查询", Id, string.Empty, Kind switch { "impact" => "graph", "tree" => "tree", _ => "table" });
}

public sealed record RunRequest(string DemoId, string QueryId, IReadOnlyList<DemoTable>? Tables = null);
public sealed record TableResult(IReadOnlyList<string> Columns, IReadOnlyList<IReadOnlyDictionary<string, object?>> Rows);
public sealed record FrontendGraph(IReadOnlyList<FrontendGraphNode> Nodes, IReadOnlyList<FrontendGraphRelationLink> RelationLinks);
public sealed record FrontendGraphNode(string Id, string Label, string? Group = null);
public sealed record FrontendGraphRelationLink(string From, string To, string? Label = null);
public sealed record FrontendTreeNode(string Id, string Label, IReadOnlyList<FrontendTreeNode>? Children = null);
