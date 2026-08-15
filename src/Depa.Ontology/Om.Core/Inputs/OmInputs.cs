using System.Collections.Immutable;
using System.Text.Json;
using Depa.Ontology.Contracts.Models;

namespace Depa.Ontology.Inputs;

public sealed record DefineClassInput(string Name, string Description, string? ParentClass = null, IReadOnlyList<string>? Mixins = null);

public sealed record DefineMixinInput(string Name, string Description);

public sealed record DefineFieldInput(
    string ClassName,
    string FieldName,
    OmValueType ValueType,
    bool Required = false,
    string? Description = null);

public sealed record DefineRelationDefInput(
    string RelationName,
    string FromClass,
    string ToClass,
    bool Directed = true,
    string? Description = null);

public sealed record ObjectInput(string Id, string ClassName, string Label);

public sealed record SetFieldValueInput(string ObjectId, string FieldName, object? Value, WriteOptions? Options = null);

public sealed record CreateRelationLinkInput(
    string FromObjectId,
    string RelationName,
    string ToObjectId,
    object? Payload = null,
    WriteOptions? Options = null);

public sealed record WriteOptions(bool SkipConstraints = false, string? ValidTime = null);

public sealed record HistoryRangeOptions(string? From = null, string? To = null);

public sealed record FindByClassCoreOptions(bool Exact = false);

public sealed record FindByClassOptions(bool Exact = false);

public sealed record DefineExistentialRuleInput(string RuleName, ExistentialRuleSpec Spec);

public sealed record CheckExistentialRulesInput(IReadOnlyList<string>? Rules = null, string? AsOf = null);

public sealed record ApplyExistentialRulesInput(IReadOnlyList<string>? Rules = null, int? MaxIterations = null, string? ValidTime = null);

public sealed record DefineConstraintInput(string ClassName, string ConstraintName, string ConstraintKind, string Message = "");

public sealed record DefineComputedPropInput(string ClassName, string ComputedPropName, string Description = "");

public sealed record DefineOperationInput(string ClassName, string OperationName, string Description = "");

public sealed record DefineMutationInput(string ClassName, string MutationName, string Description = "");

public sealed record AddInterceptorInput(string ClassName, string OperationName, string Phase, int Seq, string Description = "");

public sealed record SchemaMigrationSpec(
    string MigrationId,
    int FromVersion,
    int ToVersion,
    string? Label = null,
    string? Description = null,
    bool Strict = false,
    IReadOnlyList<JsonElement>? Steps = null);

public sealed record InitializeSchemaV2Input(SchemaInitializationOptions? Options = null)
{
    public SchemaInitializationOptions EffectiveOptions => Options ?? SchemaInitializationOptions.Default;
}

public sealed record SchemaMigrationV2Input
{
    public SchemaMigrationV2Input(
        string migrationId,
        int fromVersion,
        int toVersion,
        IEnumerable<JsonElement>? steps = null,
        string? label = null,
        string? description = null,
        SchemaMigrationV2Options? options = null)
    {
        MigrationId = migrationId;
        FromVersion = fromVersion;
        ToVersion = toVersion;
        Steps = steps?.Select(step => step.Clone()).ToImmutableArray() ?? ImmutableArray<JsonElement>.Empty;
        Label = label;
        Description = description;
        Options = options ?? SchemaMigrationV2Options.Default;
    }

    public string MigrationId { get; }
    public int FromVersion { get; }
    public int ToVersion { get; }
    public ImmutableArray<JsonElement> Steps { get; }
    public string? Label { get; }
    public string? Description { get; }
    public SchemaMigrationV2Options Options { get; }
}

public sealed record RollbackSchemaV2Input(int TargetVersion, SchemaRollbackV2Options? Options = null)
{
    public SchemaRollbackV2Options EffectiveOptions => Options ?? SchemaRollbackV2Options.Default;
}

public sealed record DefinePermissionPolicyInput(
    string PolicyId,
    string Effect,
    string Operation,
    string ResourceClass,
    bool Enabled = true,
    string Description = "");

public sealed record DefinePermissionAbacRuleInput(string PolicyId, string LeftRef, string Op, string RightRef);

public sealed record AddPermissionPathRuleInput(string PolicyId, string Path);
