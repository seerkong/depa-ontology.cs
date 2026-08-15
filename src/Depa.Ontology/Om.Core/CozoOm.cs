using System.Text.Json;
using Depa.Cozo;
using Depa.Ontology.Contracts;
using Depa.Ontology.Contracts.Models;
using Depa.Ontology.Inputs;
using Depa.Ontology.Internals;
using Depa.Ontology.Logic;
using Depa.Ontology.Runtime;
using Depa.Ontology.Support;

namespace Depa.Ontology;

public sealed class CozoOm
{
    public CozoOm(CozoDb db, CozoOmOptions? options = null)
        : this(new CozoDbOmStore(db), options)
    {
    }

    public CozoOm(ICozoOmStore store, CozoOmOptions? options = null)
    {
        Runtime = new CozoOmRuntime(
            store ?? throw new ArgumentNullException(nameof(store)),
            options ?? new CozoOmOptions(),
            new CozoOmRegistry());
    }

    public CozoOmRuntime Runtime { get; }

    public Task<BehaviorCatalog> GetBehaviorCatalogAsync(CancellationToken cancellationToken = default) =>
        BehaviorCatalogLogic.GetAsync(Runtime, cancellationToken);

    public async Task<byte[]> ExportBehaviorManifestJsonAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var catalog = await GetBehaviorCatalogAsync(cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        return BehaviorManifestJsonCodec.Encode(catalog);
    }

    public Task<BehaviorImportResult> ImportBehaviorManifestJsonAsync(
        string json,
        BehaviorCallbackBindingSet? callbacks = null,
        BehaviorImportOptions? options = null,
        CancellationToken cancellationToken = default) =>
        BehaviorImportLogic.ImportJsonAsync(Runtime, json, callbacks, options, cancellationToken);

    public async Task ClearRegistryAsync(CancellationToken cancellationToken = default)
    {
        await using var gate = await Runtime.BehaviorGate.EnterAsync(cancellationToken).ConfigureAwait(false);
        Runtime.Registry.Clear();
    }

    public Task InitSchemaAsync(CancellationToken cancellationToken = default) =>
        SchemaLogic.InitSchemaAsync(Runtime, cancellationToken);

    public Task<SchemaInitializationResult> InitializeSchemaV2Async(
        InitializeSchemaV2Input? input = null,
        CancellationToken cancellationToken = default) =>
        SchemaLogic.InitializeSchemaV2Async(Runtime, input, cancellationToken);

    public Task<SchemaMigrationV2Result> ApplySchemaMigrationV2Async(
        SchemaMigrationV2Input input,
        CancellationToken cancellationToken = default) =>
        SchemaLogic.ApplySchemaMigrationV2Async(Runtime, input, cancellationToken);

    public Task<SchemaRollbackV2Result> RollbackSchemaV2Async(
        RollbackSchemaV2Input input,
        CancellationToken cancellationToken = default) =>
        SchemaLogic.RollbackSchemaV2Async(Runtime, input, cancellationToken);

    public Task DefineClassAsync(string name, string description = "", string? parentClass = null, IReadOnlyList<string>? mixins = null, CancellationToken cancellationToken = default) =>
        ClassLogic.DefineClassAsync(Runtime, new DefineClassInput(name, description, parentClass, mixins), cancellationToken);

    public Task DefineClassAsync(DefineClassPatchInput input, CancellationToken cancellationToken = default) =>
        ClassLogic.DefineClassAsync(Runtime, input, cancellationToken);

    public Task DefineMixinAsync(string name, string description = "", CancellationToken cancellationToken = default) =>
        ClassLogic.DefineMixinAsync(Runtime, new DefineMixinInput(name, description), cancellationToken);

    public Task DefineFieldAsync(
        string className,
        string fieldName,
        OmValueType valueKind,
        bool required = false,
        string? description = null,
        CancellationToken cancellationToken = default) =>
        ClassLogic.DefineFieldAsync(Runtime, new DefineFieldInput(className, fieldName, valueKind, required, description), cancellationToken);

    public Task DefineRelationDefAsync(
        string relationName,
        string fromClass,
        string toClass,
        bool directed = true,
        string? description = null,
        CancellationToken cancellationToken = default) =>
        ClassLogic.DefineRelationDefAsync(Runtime, new DefineRelationDefInput(relationName, fromClass, toClass, directed, description), cancellationToken);

    public Task DefineClassAliasAsync(string alias, string canonical, CancellationToken cancellationToken = default) =>
        ClassLogic.DefineClassAliasAsync(Runtime, alias, canonical, cancellationToken);

    public Task DefineRelationDefAliasAsync(string alias, string canonical, CancellationToken cancellationToken = default) =>
        ClassLogic.DefineRelationDefAliasAsync(Runtime, alias, canonical, cancellationToken);

    public Task DefineFieldAliasAsync(string className, string aliasField, string canonicalField, CancellationToken cancellationToken = default) =>
        ClassLogic.DefineFieldAliasAsync(Runtime, className, aliasField, canonicalField, cancellationToken);

    public Task CreateObjectAsync(string id, string className, string label = "", CancellationToken cancellationToken = default) =>
        ObjectLogic.CreateObjectAsync(Runtime, new ObjectInput(id, className, label), cancellationToken);

    public Task UpsertObjectAsync(string id, string className, string label = "", CancellationToken cancellationToken = default) =>
        ObjectLogic.UpsertObjectAsync(Runtime, new ObjectInput(id, className, label), cancellationToken);

    public Task<string> GetObjectClassAsync(string objectId, CancellationToken cancellationToken = default) =>
        ObjectLogic.GetObjectClassAsync(Runtime, objectId, cancellationToken);

    /// <summary>
    /// Physically deletes an object in a single transaction, cascading to all of its field-value
    /// rows (every temporal version) and every relation link touching it. Deleting a
    /// non-existent object is a harmless no-op.
    /// </summary>
    public Task DeleteObjectAsync(string objectId, CancellationToken cancellationToken = default) =>
        ObjectLogic.DeleteObjectAsync(Runtime, objectId, cancellationToken);

    public Task SetFieldValueAsync(string objectId, string fieldName, object? value, WriteOptions? options = null, CancellationToken cancellationToken = default) =>
        ObjectLogic.SetFieldValueAsync(Runtime, new SetFieldValueInput(objectId, fieldName, value, options), cancellationToken);

    public Task<JsonElement?> GetFieldValueAsync(string objectId, string fieldName, CancellationToken cancellationToken = default) =>
        ObjectLogic.GetFieldValueAsync(Runtime, objectId, fieldName, cancellationToken);

    public Task<JsonElement?> GetFieldValueAsOfAsync(string objectId, string fieldName, string asOf, CancellationToken cancellationToken = default) =>
        ObjectLogic.GetFieldValueAsOfAsync(Runtime, objectId, fieldName, asOf, cancellationToken);

    public async Task<IReadOnlyList<FieldValueHistoryEntry>> GetFieldValueHistoryAsync(string objectId, string fieldName, HistoryRangeOptions? options = null, CancellationToken cancellationToken = default)
    {
        var entries = await ObjectLogic.GetFieldValueHistoryAsync(Runtime, objectId, fieldName, options, cancellationToken);
        return entries.Select(ToFieldValueHistoryEntry).ToArray();
    }

    public Task CreateRelationLinkAsync(string fromObjectId, string relationName, string toObjectId, object? payload = null, WriteOptions? options = null, CancellationToken cancellationToken = default) =>
        RelationLogic.CreateRelationLinkAsync(Runtime, new CreateRelationLinkInput(fromObjectId, relationName, toObjectId, payload, options), cancellationToken);

    public Task RetractRelationLinkAsync(string fromObjectId, string relationName, string toObjectId, WriteOptions? options = null, CancellationToken cancellationToken = default) =>
        RelationLogic.RetractRelationLinkAsync(Runtime, fromObjectId, relationName, toObjectId, options, cancellationToken);

    public Task<NeighborResult> GetNeighborsAsync(string objectId, string? relationName = null, OmDirection direction = OmDirection.Both, CancellationToken cancellationToken = default) =>
        RelationLogic.GetNeighborsAsync(Runtime, objectId, relationName, direction, cancellationToken);

    public Task<NeighborResult> GetNeighborsAsOfAsync(string objectId, string? relationName, string asOf, OmDirection direction = OmDirection.Both, CancellationToken cancellationToken = default) =>
        RelationLogic.GetNeighborsAsOfAsync(Runtime, objectId, relationName, asOf, direction, cancellationToken);

    public async Task<IReadOnlyList<OmObject>> TraverseAsync(
        string startObjectId,
        IReadOnlyList<string>? relationPath,
        CancellationToken cancellationToken = default)
    {
        var objects = await RelationLogic.TraverseAsync(Runtime, startObjectId, relationPath, cancellationToken);
        return objects.Select(ToObject).ToArray();
    }

    public Task<IReadOnlyList<RelationLinkHistoryEntry>> GetRelationLinkHistoryAsync(string fromObjectId, string relationName, string? toObjectId = null, HistoryRangeOptions? options = null, CancellationToken cancellationToken = default) =>
        RelationLogic.GetRelationLinkHistoryAsync(Runtime, fromObjectId, relationName, toObjectId, options, cancellationToken);

    public async Task<ObjectView?> GetObjectViewAsync(string objectId, CancellationToken cancellationToken = default) =>
        ToObjectView(await ObjectLogic.GetObjectViewRowAsync(Runtime, objectId, cancellationToken));

    public async Task<ObjectView?> GetObjectViewAsOfAsync(string objectId, string asOf, CancellationToken cancellationToken = default) =>
        ToObjectView(await ObjectLogic.GetObjectViewRowAsOfAsync(Runtime, objectId, asOf, cancellationToken));

    public async Task<IReadOnlyList<OmObject>> FindByClassAsync(string className, FindByClassOptions? options = null, CancellationToken cancellationToken = default)
    {
        var objects = await ObjectLogic.FindByClassAsync(Runtime, className, ToFindByClassCoreOptions(options), cancellationToken);
        return objects.Select(ToObject).ToArray();
    }

    public async Task<IReadOnlyList<FindByClassEntry>> FindByClassWithFieldValuesAsync(
        string className,
        IReadOnlyDictionary<string, object?>? filter = null,
        FindByClassOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var entries = await ObjectLogic.FindByClassWithFieldValuesAsync(Runtime, className, filter, ToFindByClassCoreOptions(options), cancellationToken);
        return entries.Select(ToFindByClassEntry).ToArray();
    }

    public Task<double> AggregateByClassAsync(string className, string fieldName, string op, FindByClassOptions? options = null, CancellationToken cancellationToken = default) =>
        ObjectLogic.AggregateByClassAsync(Runtime, className, fieldName, op, ToFindByClassCoreOptions(options), cancellationToken);

    public Task<IReadOnlyList<string>> GetAncestorsAsync(string className, CancellationToken cancellationToken = default) =>
        ClassLogic.GetAncestorsAsync(Runtime, className, cancellationToken);

    public Task<IReadOnlyList<string>> GetDescendantsAsync(string className, CancellationToken cancellationToken = default) =>
        ClassLogic.GetDescendantsAsync(Runtime, className, cancellationToken);

    public Task<bool> IsSubclassOfAsync(string childClass, string parentClass, CancellationToken cancellationToken = default) =>
        ClassLogic.IsSubclassOfAsync(Runtime, childClass, parentClass, cancellationToken);

    public async Task<ClassHierarchy> GetClassHierarchyAsync(CancellationToken cancellationToken = default) =>
        ToClassHierarchy(await ClassLogic.GetClassHierarchyRowAsync(Runtime, cancellationToken));

    public Task<string> ResolveClassAsync(string className, CancellationToken cancellationToken = default) =>
        ClassLogic.ResolveClassAsync(Runtime, className, cancellationToken);

    public Task<string> ResolveRelationAsync(string relationName, CancellationToken cancellationToken = default) =>
        ClassLogic.ResolveRelationAsync(Runtime, relationName, cancellationToken);

    public Task<string> ResolveFieldAsync(string className, string fieldName, CancellationToken cancellationToken = default) =>
        ClassLogic.ResolveFieldAsync(Runtime, className, fieldName, cancellationToken);

    public async Task<IReadOnlyDictionary<string, OmField>> GetFieldDefinitionsAsync(string className, CancellationToken cancellationToken = default)
    {
        var definitions = await ClassLogic.GetFieldDefinitionsAsync(Runtime, className, cancellationToken);
        return definitions.ToDictionary(pair => pair.Key, pair => ToField(pair.Value), StringComparer.Ordinal);
    }

    public Task<ValidationResult> ValidateObjectAsync(string objectId, CancellationToken cancellationToken = default) =>
        ConstraintLogic.ValidateObjectAsync(Runtime, objectId, cancellationToken);

    public Task<ValidationResult> ValidateConstraintsAsync(
        string objectId,
        IReadOnlyList<string>? types = null,
        CancellationToken cancellationToken = default) =>
        ConstraintLogic.ValidateConstraintsAsync(Runtime, objectId, types, cancellationToken);

    public OmValueType InferValueType(object? value) => OmConvert.InferValueType(value);

    public Task ValidateFieldValueTypeAsync(string objectId, string fieldName, object? value, CancellationToken cancellationToken = default) =>
        ObjectLogic.ValidateFieldValueTypeAsync(Runtime, objectId, fieldName, value, cancellationToken);

    public Task ValidateRelationAsync(string fromId, string relationName, string toId, CancellationToken cancellationToken = default) =>
        RelationLogic.ValidateRelationAsync(Runtime, fromId, relationName, toId, cancellationToken);

    public Task<IReadOnlyList<string>> ValidateRequiredFieldValuesAsync(string objectId, CancellationToken cancellationToken = default) =>
        ConstraintLogic.ValidateRequiredFieldValuesAsync(Runtime, objectId, cancellationToken);

    public Task FinalizeObjectAsync(string objectId, CancellationToken cancellationToken = default) =>
        ConstraintLogic.FinalizeObjectAsync(Runtime, objectId, cancellationToken);

    public Task DefineConstraintAsync(string className, string constraintName, string constraintKind, string message = "", CancellationToken cancellationToken = default) =>
        ConstraintLogic.DefineConstraintAsync(Runtime, new DefineConstraintInput(className, constraintName, constraintKind, message), cancellationToken);

    public async Task DefineConstraintAsync(
        string className,
        string constraintName,
        string constraintKind,
        Func<OmValidationContext, ValueTask<bool>> when,
        Func<OmValidationContext, ValueTask<bool>> then,
        string message = "",
        CancellationToken cancellationToken = default)
    {
        await ConstraintLogic.DefineConstraintCallbackAsync(
            Runtime,
            new DefineConstraintInput(className, constraintName, constraintKind, message),
            when,
            then,
            cancellationToken: cancellationToken);
    }

    public void RegisterValidator(string className, string constraintName, Func<OmValidationContext, ValueTask<string?>> validator) =>
        Runtime.Registry.RegisterValidator(className, constraintName, validator);

    public void RegisterConstraint(
        string className,
        string constraintName,
        Func<OmValidationContext, ValueTask<bool>> when,
        Func<OmValidationContext, ValueTask<bool>> then) =>
        Runtime.Registry.RegisterConstraint(className, constraintName, when, then);

    public Task DefineComputedPropAsync(string className, string computedPropName, string description = "", CancellationToken cancellationToken = default) =>
        ConstraintLogic.DefineComputedPropAsync(Runtime, new DefineComputedPropInput(className, computedPropName, description), cancellationToken);

    public void RegisterComputedProp(string className, string computedPropName, Func<OmComputedPropContext, ValueTask<object?>> compute) =>
        Runtime.Registry.RegisterComputedProp(className, computedPropName, compute);

    public Task DefineOperationAsync(string className, string operationName, string description = "", CancellationToken cancellationToken = default) =>
        ConstraintLogic.DefineOperationAsync(Runtime, new DefineOperationInput(className, operationName, description), cancellationToken);

    public Task DefineMutationAsync(string className, string mutationName, string description = "", CancellationToken cancellationToken = default) =>
        ConstraintLogic.DefineMutationAsync(Runtime, new DefineMutationInput(className, mutationName, description), cancellationToken);

    public Task AddInterceptorAsync(string className, string operationName, string phase, int seq, string description = "", CancellationToken cancellationToken = default) =>
        ConstraintLogic.AddInterceptorAsync(Runtime, new AddInterceptorInput(className, operationName, phase, seq, description), cancellationToken);

    public async Task DefineOperationAsync(
        string className,
        string operationName,
        Func<OmOperationContext, IReadOnlyDictionary<string, object?>, ValueTask<IReadOnlyList<MutationSpec>>> handler,
        string description = "",
        CancellationToken cancellationToken = default)
    {
        await ConstraintLogic.DefineOperationCallbackAsync(
            Runtime,
            new DefineOperationInput(className, operationName, description),
            handler,
            cancellationToken: cancellationToken);
    }

    public async Task DefineMutationAsync(
        string className,
        string mutationName,
        Func<OmMutationContext, IReadOnlyDictionary<string, object?>, ValueTask> executor,
        string description = "",
        CancellationToken cancellationToken = default)
    {
        await ConstraintLogic.DefineMutationCallbackAsync(
            Runtime,
            new DefineMutationInput(className, mutationName, description),
            executor,
            cancellationToken: cancellationToken);
    }

    public async Task AddInterceptorAsync(
        string className,
        string operationName,
        string phase,
        Func<OmOperationContext, ValueTask> handler,
        string description = "",
        CancellationToken cancellationToken = default)
    {
        await ConstraintLogic.AddInterceptorCallbackAsync(
            Runtime,
            new AddInterceptorInput(className, operationName, phase, Seq: 0, description),
            handler,
            cancellationToken: cancellationToken);
    }

    public Task ExecuteOperationAsync(
        string objectId,
        string operationName,
        IReadOnlyDictionary<string, object?>? parameters = null,
        CancellationToken cancellationToken = default) =>
        ConstraintLogic.ExecuteOperationAsync(Runtime, objectId, operationName, parameters, cancellationToken);

    public Task ExecuteMutationsAsync(
        string objectId,
        IReadOnlyList<MutationSpec>? mutations,
        CancellationToken cancellationToken = default) =>
        ConstraintLogic.ExecuteMutationsAsync(Runtime, objectId, mutations, cancellationToken);

    public Task<SchemaState> GetSchemaStateAsync(CancellationToken cancellationToken = default) =>
        SchemaLogic.GetSchemaStateAsync(Runtime, cancellationToken);

    public Task<IReadOnlyList<SchemaVersion>> ListSchemaVersionsAsync(CancellationToken cancellationToken = default) =>
        SchemaLogic.ListSchemaVersionsAsync(Runtime, cancellationToken);

    public Task<SchemaSnapshot> WriteSchemaSnapshotAsync(int version, string? label = null, string? description = null, CancellationToken cancellationToken = default) =>
        SchemaLogic.WriteSchemaSnapshotAsync(Runtime, version, label, description, cancellationToken);

    public Task<SchemaSnapshot?> ReadSchemaSnapshotAsync(int version, CancellationToken cancellationToken = default) =>
        SchemaLogic.ReadSchemaSnapshotAsync(Runtime, version, cancellationToken);

    /// <summary>
    /// Returns the legacy coarse snapshot diff. Use DiffSchemaVersionsV2Async
    /// when callers need a per-definition keyed result.
    /// </summary>
    public Task<SchemaDiff> DiffSchemaVersionsAsync(int fromVersion, int toVersion, CancellationToken cancellationToken = default) =>
        SchemaLogic.DiffSchemaVersionsAsync(Runtime, fromVersion, toVersion, cancellationToken);

    public Task<SchemaDiff> DiffCurrentAgainstSnapshotAsync(int fromVersion, CancellationToken cancellationToken = default) =>
        SchemaLogic.DiffCurrentAgainstSnapshotAsync(Runtime, fromVersion, cancellationToken);

    public Task<SchemaKeyedDiff> DiffSchemaVersionsV2Async(int fromVersion, int toVersion, CancellationToken cancellationToken = default) =>
        SchemaLogic.DiffSchemaVersionsV2Async(Runtime, fromVersion, toVersion, cancellationToken);

    public Task<SchemaKeyedDiff> DiffCurrentAgainstSnapshotV2Async(int fromVersion, CancellationToken cancellationToken = default) =>
        SchemaLogic.DiffCurrentAgainstSnapshotV2Async(Runtime, fromVersion, cancellationToken);

    /// <summary>
    /// Compatibility rollback API. Its legacy strict argument is not a V2
    /// validation guarantee and therefore remains separate from RollbackSchemaV2Input.
    /// </summary>
    public Task RollbackSchemaAsync(int version, bool strict = false, CancellationToken cancellationToken = default) =>
        SchemaLogic.RollbackSchemaAsync(Runtime, version, strict, cancellationToken);

    /// <summary>
    /// Compatibility migration API. Use ApplySchemaMigrationV2Async for the
    /// strict-by-default, atomic migration contract.
    /// </summary>
    public Task<SchemaMigrationResult> ApplySchemaMigrationAsync(SchemaMigrationSpec spec, CancellationToken cancellationToken = default) =>
        SchemaLogic.ApplySchemaMigrationAsync(Runtime, spec, cancellationToken);

    public Task<ExistentialRule> DefineExistentialRuleAsync(string ruleName, ExistentialRuleSpec spec, CancellationToken cancellationToken = default) =>
        ExistentialRuleLogic.DefineExistentialRuleAsync(Runtime, new DefineExistentialRuleInput(ruleName, spec), cancellationToken);

    public Task<IReadOnlyList<ExistentialRule>> ListExistentialRulesAsync(CancellationToken cancellationToken = default) =>
        ExistentialRuleLogic.ListExistentialRulesAsync(Runtime, cancellationToken);

    public Task<IReadOnlyList<ExistentialViolation>> CheckExistentialRulesAsync(CheckExistentialRulesInput? input = null, CancellationToken cancellationToken = default) =>
        ExistentialRuleLogic.CheckExistentialRulesAsync(Runtime, input, cancellationToken);

    public Task<ExistentialChaseResult> ApplyExistentialRulesAsync(ApplyExistentialRulesInput? input = null, CancellationToken cancellationToken = default) =>
        ExistentialRuleLogic.ApplyExistentialRulesAsync(Runtime, input, cancellationToken);

    public Task SeedPermissionMetadataAsync(CancellationToken cancellationToken = default) =>
        ConstraintLogic.SeedPermissionMetadataAsync(Runtime, cancellationToken);

    public Task SeedPermissionMetadataAsync(PermissionSeedInput input, CancellationToken cancellationToken = default) =>
        ConstraintLogic.SeedPermissionMetadataAsync(Runtime, input, cancellationToken);

    public Task DefinePermissionPolicyAsync(
        string policyId,
        string effect,
        string operation,
        string resourceClass,
        bool enabled = true,
        string description = "",
        CancellationToken cancellationToken = default) =>
        ConstraintLogic.DefinePermissionPolicyAsync(Runtime, new DefinePermissionPolicyInput(policyId, effect, operation, resourceClass, enabled, description), cancellationToken);

    public Task AddPermissionAbacRuleAsync(
        string policyId,
        string leftRef,
        string op,
        string rightRef,
        CancellationToken cancellationToken = default) =>
        ConstraintLogic.AddPermissionAbacRuleAsync(Runtime, new DefinePermissionAbacRuleInput(policyId, leftRef, op, rightRef), cancellationToken);

    public Task AddPermissionPathRuleAsync(string policyId, string path, CancellationToken cancellationToken = default) =>
        ConstraintLogic.AddPermissionPathRuleAsync(Runtime, new AddPermissionPathRuleInput(policyId, path), cancellationToken);

    public Task<CheckAccessResult> CheckAccessAsync(CheckAccessInput input, CancellationToken cancellationToken = default) =>
        ConstraintLogic.CheckAccessAsync(Runtime, input, cancellationToken);

    private static FindByClassCoreOptions? ToFindByClassCoreOptions(FindByClassOptions? options) =>
        options is null ? null : new FindByClassCoreOptions(options.Exact);

    private static OmObject ToObject(OmObjectRow entity) =>
        new(entity.Id, entity.ClassName, entity.Label);

    private static OmField ToField(OmFieldDefinition field) =>
        new(field.ClassName, field.FieldName, field.ValueType, field.Required, field.Description);

    private static FieldValueHistoryEntry ToFieldValueHistoryEntry(FieldValueHistoryRow entry) =>
        new(entry.Value, entry.ValidTime, entry.TxTime);

    private static ObjectView? ToObjectView(ObjectViewRow? view) =>
        view is null
            ? null
            : new ObjectView(
                view.Id,
                view.ClassName,
                view.Label,
                view.FieldValues,
                view.Outgoing.Select(link => new ObjectViewEdge(link.RelationName, link.ToObjectId, link.ToClass, link.ToLabel)).ToArray());

    private static FindByClassEntry ToFindByClassEntry(FindByClassEntryRow entry) =>
        new(entry.Id, entry.ClassName, entry.Label, entry.FieldValues);

    private static ClassHierarchy ToClassHierarchy(ClassHierarchyRow hierarchy) =>
        new(
            hierarchy.Types.ToDictionary(
                pair => pair.Key,
                pair => new ClassHierarchyNode(
                    pair.Value.Name,
                    pair.Value.Description,
                    pair.Value.ParentClass,
                    pair.Value.Mixins,
                    pair.Value.Children),
                StringComparer.Ordinal),
            hierarchy.Roots);
}
