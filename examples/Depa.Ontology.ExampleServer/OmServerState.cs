using System.Text.Json;
using Depa.Cozo;
using Depa.Ontology.Contracts.Models;
using Depa.Ontology.Inputs;

namespace Depa.Ontology.ExampleServer;

/// <summary>Owns independently serialized mutable databases used by the server demos.</summary>
public sealed class OmServerState : IDisposable
{
    public StatefulOmDomain Schema { get; } = new();
    public StatefulOmDomain Governance { get; } = new();
    public StatefulOmDomain Integrity { get; } = new();

    public void Dispose()
    {
        Schema.Dispose();
        Governance.Dispose();
        Integrity.Dispose();
    }
}

public sealed class StatefulOmDomain : IDisposable
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private CozoDb _db = NewDb();

    public async Task<T> UseAsync<T>(Func<CozoOm, Task<T>> action)
    {
        await _gate.WaitAsync();
        try { return await action(new CozoOm(_db)); }
        finally { _gate.Release(); }
    }

    public async Task<T> ResetAsync<T>(Func<CozoOm, Task<T>> seed)
    {
        await _gate.WaitAsync();
        CozoDb? next = null;
        try
        {
            next = NewDb();
            var result = await seed(new CozoOm(next));
            var previous = _db;
            _db = next;
            next = null;
            previous.Dispose();
            return result;
        }
        finally
        {
            next?.Dispose();
            _gate.Release();
        }
    }

    public void Dispose()
    {
        _gate.Wait();
        try { _db.Dispose(); }
        finally { _gate.Release(); _gate.Dispose(); }
    }

    private static CozoDb NewDb() => new("mem", "");
}

public sealed record SchemaDiffRequest(int FromVersion, int ToVersion);
public sealed record SchemaApplyRequest(JsonElement Spec);
public sealed record SchemaRollbackRequest(int TargetVersion, bool Strict = true);
public sealed record GovernanceAccessRequest(string SubjectId, string Action, string ResourceId, string? FieldName = null);

public static class OmServerEndpoints
{
    private const string IntegrityRuleName = "resource_must_have_owner";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static Task<object> SchemaStateAsync(OmServerState state) => state.Schema.UseAsync(async om =>
    {
        await om.InitSchemaAsync();
        return (object)await om.GetSchemaStateAsync();
    });

    public static Task<object> SchemaVersionsAsync(OmServerState state) => state.Schema.UseAsync(async om =>
    {
        await om.InitSchemaAsync();
        var details = await om.ListSchemaVersionsAsync();
        return (object)new { versions = details, versionNumbers = details.Select(version => version.Version).ToArray() };
    });

    public static Task<object> SchemaDiffAsync(OmServerState state, SchemaDiffRequest request) => state.Schema.UseAsync(async om =>
    {
        await om.InitSchemaAsync();
        return (object)new { diff = await om.DiffSchemaVersionsAsync(request.FromVersion, request.ToVersion) };
    });

    public static Task<object> SchemaApplyAsync(OmServerState state, SchemaApplyRequest request) => state.Schema.UseAsync(async om =>
    {
        await om.InitSchemaAsync();
        var spec = JsonSerializer.Deserialize<SchemaMigrationSpec>(request.Spec.GetRawText(), JsonOptions)
            ?? throw new InvalidOperationException("Invalid schema migration spec");
        var result = await om.ApplySchemaMigrationAsync(spec);
        return (object)new { ok = true, result, state = await om.GetSchemaStateAsync() };
    });

    public static Task<object> SchemaRollbackAsync(OmServerState state, SchemaRollbackRequest request) => state.Schema.UseAsync(async om =>
    {
        await om.InitSchemaAsync();
        await om.RollbackSchemaAsync(request.TargetVersion, request.Strict);
        return (object)new { ok = true, result = new { ok = true, targetVersion = request.TargetVersion }, state = await om.GetSchemaStateAsync() };
    });

    public static object GovernanceSeedTemplate() => new { ok = true, tables = GovernanceTables(), subjectId = "u:1", resourceId = "r:1", action = "read" };

    public static Task<object> GovernanceSeedAsync(OmServerState state) => state.Governance.ResetAsync(async om =>
    {
        await SeedGovernanceAsync(om);
        return (object)new { ok = true, subjectId = "u:1", resourceId = "r:1", action = "read" };
    });

    public static Task<object> GovernanceCheckAsync(OmServerState state, GovernanceAccessRequest request) => state.Governance.UseAsync(async om =>
    {
        await EnsureGovernanceAsync(om);
        return (object)new { result = await om.CheckAccessAsync(new CheckAccessInput(request.SubjectId, request.Action, request.ResourceId, FieldName: request.FieldName)) };
    });

    public static Task<object> IntegritySeedAsync(OmServerState state) => state.Integrity.ResetAsync(async om =>
    {
        await SeedIntegrityAsync(om);
        return (object)new { ok = true, rules = await om.ListExistentialRulesAsync() };
    });

    public static Task<object> IntegrityRulesAsync(OmServerState state) => state.Integrity.UseAsync(async om =>
    {
        await EnsureIntegrityAsync(om);
        return (object)new { rules = await om.ListExistentialRulesAsync() };
    });

    public static Task<object> IntegrityCheckAsync(OmServerState state, CheckExistentialRulesInput? request = null) => state.Integrity.UseAsync(async om =>
    {
        await EnsureIntegrityAsync(om);
        return (object)new { violations = await om.CheckExistentialRulesAsync(request) };
    });

    public static Task<object> IntegrityApplyAsync(OmServerState state, ApplyExistentialRulesInput? request = null) => state.Integrity.UseAsync(async om =>
    {
        await EnsureIntegrityAsync(om);
        return (object)new { ok = true, result = await om.ApplyExistentialRulesAsync(request) };
    });

    private static async Task EnsureGovernanceAsync(CozoOm om)
    {
        await om.InitSchemaAsync();
        try { await om.GetEntityTypeAsync("u:1"); }
        catch { await SeedGovernanceAsync(om); }
    }

    private static async Task SeedGovernanceAsync(CozoOm om)
    {
        await om.InitSchemaAsync();
        await om.DefineTypeAsync("User", "User");
        await om.DefineTypeAsync("Resource", "Resource");
        await om.DefineAttributeAsync("User", "role", OmValueType.String);
        await om.DefineRelationAsync("owns", "User", "Resource");
        await om.CreateEntityAsync("u:1", "User", "User 1");
        await om.CreateEntityAsync("r:1", "Resource", "Resource 1");
        await om.SetPropertyAsync("u:1", "role", "admin");
        await om.LinkEntitiesAsync("u:1", "owns", "r:1");
        await om.SeedPermissionMetadataAsync(new PermissionSeedInput(
            Actions: [new PermissionActionSeed("read", "Read")],
            Policies: [new PermissionPolicySeed("allow-admin-owner", "allow", "read", "Resource")],
            AbacRules: [new PermissionAbacRuleSeed("allow-admin-owner", "subject.role", "=", "admin")],
            PathRules: [new PermissionPathRuleSeed("allow-admin-owner", "owns")]));
    }

    private static async Task EnsureIntegrityAsync(CozoOm om)
    {
        await om.InitSchemaAsync();
        if (!(await om.ListExistentialRulesAsync()).Any(rule => rule.RuleName == IntegrityRuleName)) await SeedIntegrityAsync(om);
    }

    private static async Task SeedIntegrityAsync(CozoOm om)
    {
        await om.InitSchemaAsync();
        await om.DefineTypeAsync("User", "User");
        await om.DefineTypeAsync("Resource", "Resource");
        await om.DefineRelationAsync("owns", "User", "Resource");
        await om.DefineExistentialRuleAsync(IntegrityRuleName, new ExistentialRuleSpec(
            new ExistentialForEachSpec("Resource"),
            new ExistentialExistsSpec("owns", ExistentialDirection.In, "User"),
            new ExistentialMaterializeSpec("auto owner for {fromId}"),
            ExistentialRuleMode.Materialize,
            "每个资源必须有归属用户"));
        await om.CreateEntityAsync("r:unowned-1", "Resource", "Unowned Resource 1");
        await om.CreateEntityAsync("r:unowned-2", "Resource", "Unowned Resource 2");
    }

    private static IReadOnlyList<DemoTable> GovernanceTables() =>
    [
        new("类型定义", ["typeName", "description"], [Row(("typeName", "User"), ("description", "User")), Row(("typeName", "Resource"), ("description", "Resource"))]),
        new("实体数据", ["id", "typeName", "label"], [Row(("id", "u:1"), ("typeName", "User"), ("label", "User 1")), Row(("id", "r:1"), ("typeName", "Resource"), ("label", "Resource 1"))])
    ];

    private static IReadOnlyDictionary<string, object?> Row(params (string Key, object? Value)[] values) => values.ToDictionary(value => value.Key, value => value.Value);
}
