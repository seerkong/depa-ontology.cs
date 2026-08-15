using System.Collections.Immutable;
using System.Text;
using System.Text.Json;
using System.Xml.Linq;
using Depa.Cozo;
using Depa.Ontology;
using Depa.Ontology.Contracts;
using Depa.Ontology.Contracts.Models;
using Depa.Ontology.Inputs;
using Depa.Ontology.Logic;
using Depa.Ontology.Runtime;
using Depa.Ontology.Scripting.Jint;
using Depa.Ontology.Support;

public static class OmScriptingJintContractTests
{
    private static readonly TimeSpan OuterGuardTimeout = TimeSpan.FromSeconds(3);

    public static async Task RunAsync()
    {
        RunStep(nameof(AssertPackageIsolation), AssertPackageIsolation);
        RunStep(nameof(AssertImmutableContractsAndOrdinalBindingIds), AssertImmutableContractsAndOrdinalBindingIds);
        RunStep(nameof(AssertExactCallbackSlotMapping), AssertExactCallbackSlotMapping);
        RunStep(nameof(AssertMissingAndConflictingDefinitionsAreDeterministic), AssertMissingAndConflictingDefinitionsAreDeterministic);
        await RunStepAsync(nameof(AssertCrossSlotReuseRejectsAndSameSlotReuseSharesAsync), AssertCrossSlotReuseRejectsAndSameSlotReuseSharesAsync);
        RunStep(nameof(AssertNativeAndScriptBindingsMergeDeterministically), AssertNativeAndScriptBindingsMergeDeterministically);
        await RunStepAsync(nameof(AssertProviderValidationHasNoOmEffectsAsync), AssertProviderValidationHasNoOmEffectsAsync);
        await RunStepAsync(nameof(AssertRequireReadyBindingsExecuteScriptsAsync), AssertRequireReadyBindingsExecuteScriptsAsync);
        await RunStepAsync(nameof(AssertDirectInputPreflightIsStructuredAndEffectFreeAsync), AssertDirectInputPreflightIsStructuredAndEffectFreeAsync);
        await RunStepAsync(nameof(AssertCallablePreflightRejectsInvalidSourcesBeforeReadyImportAsync), AssertCallablePreflightRejectsInvalidSourcesBeforeReadyImportAsync);
        await RunStepAsync(nameof(AssertCallablePreflightIsResourceBoundedAndEffectFreeAsync), AssertCallablePreflightIsResourceBoundedAndEffectFreeAsync);
        await RunStepAsync(nameof(AssertCallablePreflightDoesNotExecuteFunctionBodyAsync), AssertCallablePreflightDoesNotExecuteFunctionBodyAsync);
        RunStep(nameof(AssertRecordCopyAndNullElementsReceiveDiagnosticsInsteadOfExceptions), AssertRecordCopyAndNullElementsReceiveDiagnosticsInsteadOfExceptions);
        RunStep(nameof(AssertStableOrderingAcrossReversedInputs), AssertStableOrderingAcrossReversedInputs);
        await RunStepAsync(nameof(AssertP2CallbackResultShapesRemainExactAsync), AssertP2CallbackResultShapesRemainExactAsync);
        await RunStepAsync(nameof(AssertP2InvalidResultMatrixUsesStructuredScriptExceptionsAsync), AssertP2InvalidResultMatrixUsesStructuredScriptExceptionsAsync);
        await RunStepAsync(nameof(AssertP2RuntimeAndConversionFailuresAreSourceRedactedAsync), AssertP2RuntimeAndConversionFailuresAreSourceRedactedAsync);
        await RunStepAsync(nameof(AssertP2FiniteDefaultsBoundPendingPromisesAsync), AssertP2FiniteDefaultsBoundPendingPromisesAsync);
        await RunStepAsync(nameof(AssertP2LimitsAreStableBindingAwareFailuresAsync), AssertP2LimitsAreStableBindingAwareFailuresAsync);
        RunStep(nameof(AssertP2InvalidFiniteOptionsAreRejectedBeforeExecution), AssertP2InvalidFiniteOptionsAreRejectedBeforeExecution);
        await RunStepAsync(nameof(AssertP2CancellationUsesOperationCanceledSemanticsAsync), AssertP2CancellationUsesOperationCanceledSemanticsAsync);
        await RunStepAsync(nameof(AssertP2ConcurrentInvocationsUseFreshEnginesAsync), AssertP2ConcurrentInvocationsUseFreshEnginesAsync);
        RunStep(nameof(AssertP2ProviderDoesNotSynchronouslyBlockHostTasks), AssertP2ProviderDoesNotSynchronouslyBlockHostTasks);
        await RunStepAsync(nameof(AssertP2HostReadCapabilitiesAndAmbientDenialAsync), AssertP2HostReadCapabilitiesAndAmbientDenialAsync);
        await RunStepAsync(nameof(AssertP2HostWriteCapabilitiesAndParentCompositionAsync), AssertP2HostWriteCapabilitiesAndParentCompositionAsync);
        await RunStepAsync(nameof(AssertP2HostFailuresAreStructuredAndRedactedAsync), AssertP2HostFailuresAreStructuredAndRedactedAsync);
        await RunStepAsync(nameof(AssertP2HostCancellationDoesNotPublishLateWritesAsync), AssertP2HostCancellationDoesNotPublishLateWritesAsync);
        await RunStepAsync(nameof(AssertP3CanonicalManifestImportExecutesScriptAndNativeCallbacksAsync), AssertP3CanonicalManifestImportExecutesScriptAndNativeCallbacksAsync);
        await RunStepAsync(nameof(AssertP3RequireReadyFailuresAreAtomicAsync), AssertP3RequireReadyFailuresAreAtomicAsync);
        await RunStepAsync(nameof(AssertP3RestartRequiresExactRebindWithoutPersistingSourcesAsync), AssertP3RestartRequiresExactRebindWithoutPersistingSourcesAsync);
        await RunStepAsync(nameof(AssertP3SandboxAndResourceFailuresRemainStableAfterReadyImportAsync), AssertP3SandboxAndResourceFailuresRemainStableAfterReadyImportAsync);
        await RunStepAsync(nameof(AssertOuterGuardPreservesCompletionAndTimeoutSemanticsAsync), AssertOuterGuardPreservesCompletionAndTimeoutSemanticsAsync);
        await RunStepAsync(nameof(AssertP3ParentInheritanceAndTransactionCompositionAsync), AssertP3ParentInheritanceAndTransactionCompositionAsync);
        await RunStepAsync(nameof(AssertP3ActionFailureMatrixRollsBackEntireTransactionAsync), AssertP3ActionFailureMatrixRollsBackEntireTransactionAsync);
        await RunStepAsync(nameof(AssertP3CancellationRollsBackWithoutLateEffectsAsync), AssertP3CancellationRollsBackWithoutLateEffectsAsync);
    }

    private static void RunStep(string name, Action test)
    {
        HarnessDiagnostics.Start($"Jint.{name}");
        test();
    }

    private static async Task RunStepAsync(string name, Func<Task> test)
    {
        HarnessDiagnostics.Start($"Jint.{name}");
        await test();
    }

    private static void AssertPackageIsolation()
    {
        var dotnetRoot = FindRepoRoot();
        var testProject = XDocument.Load(Path.Combine(dotnetRoot, "tests", "Depa.Ontology.Tests", "Depa.Ontology.Tests.csproj"));
        Assert(HasProjectReference(testProject, "../../src/Depa.Ontology.Scripting.Jint/Depa.Ontology.Scripting.Jint.csproj"),
            "OM tests must reference the optional Jint adapter project, not Jint directly.");

        var projectFiles = Directory.EnumerateFiles(dotnetRoot, "*.csproj", SearchOption.AllDirectories)
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToImmutableArray();
        var jintProjects = projectFiles
            .Where(path => XDocument.Load(path).Descendants()
                .Any(element => element.Name.LocalName == "PackageReference"
                                && string.Equals((string?)element.Attribute("Include"), "Jint", StringComparison.Ordinal)))
            .Select(path => Path.GetRelativePath(dotnetRoot, path).Replace(Path.DirectorySeparatorChar, '/'))
            .ToImmutableArray();
        Assert(jintProjects.SequenceEqual(["src/Depa.Ontology.Scripting.Jint/Depa.Ontology.Scripting.Jint.csproj"]),
            "Jint must be referenced only by src/Depa.Ontology.Scripting.Jint.");

        var adapterProject = XDocument.Load(Path.Combine(dotnetRoot, "src", "Depa.Ontology.Scripting.Jint", "Depa.Ontology.Scripting.Jint.csproj"));
        var jintReference = adapterProject.Descendants()
            .Single(element => element.Name.LocalName == "PackageReference"
                               && string.Equals((string?)element.Attribute("Include"), "Jint", StringComparison.Ordinal));
        Assert(string.Equals((string?)jintReference.Attribute("Version"), "4.13.0", StringComparison.Ordinal),
            "Depa.Ontology.Scripting.Jint must pin Jint 4.13.0.");

        var rootProjectText = File.ReadAllText(Path.Combine(dotnetRoot, "src", "Depa.Ontology", "Depa.Ontology.csproj"));
        Assert(!rootProjectText.Contains("Jint", StringComparison.Ordinal),
            "Depa.Ontology must remain Jint-free.");

        var omCoreSource = Directory.EnumerateFiles(Path.Combine(dotnetRoot, "src", "Depa.Ontology", "Om.Core"), "*.cs", SearchOption.AllDirectories)
            .Select(File.ReadAllText)
            .ToImmutableArray();
        Assert(!omCoreSource.Any(text => text.Contains("Jint", StringComparison.Ordinal)
                                         || text.Contains("Esprima", StringComparison.Ordinal)
                                         || text.Contains("BehaviorScript", StringComparison.Ordinal)
                                         || text.Contains("ScriptDefinition", StringComparison.Ordinal)),
            "Om.Core source must not take a JavaScript engine dependency or define a script schema.");
        Assert(!typeof(BehaviorCatalogEntry).GetProperties().Any(property => property.Name.Contains("Script", StringComparison.Ordinal))
               && !typeof(BehaviorCallbackBinding).GetProperties().Any(property => property.Name.Contains("Script", StringComparison.Ordinal)),
            "canonical behavior catalog/import contracts must keep script source out of the serialized metadata schema.");
    }

    private static void AssertImmutableContractsAndOrdinalBindingIds()
    {
        var definitions = new List<JintBehaviorScriptDefinition>
        {
            new("binding:case-sensitive", "() => true", "lower.js"),
        };
        var request = new JintBehaviorScriptBindingRequest(
            new BehaviorCatalog([
                Constraint("OrdinalOwner", "lower_constraint", BehaviorCatalogCallbackSlot.When, "binding:case-sensitive"),
                Constraint("OrdinalOwner", "upper_constraint", BehaviorCatalogCallbackSlot.When, "BINDING:case-sensitive"),
            ]),
            definitions,
            new JintBehaviorScriptOptions(
                Timeout: TimeSpan.FromSeconds(2),
                MaxStatements: 1000,
                MaxRecursionDepth: 32,
                MemoryLimitBytes: 4 * 1024 * 1024));
        definitions[0] = new("binding:case-sensitive", "() => false", "mutated.js");
        definitions.Add(new("BINDING:case-sensitive", "() => true", "late.js"));

        Assert(request.Definitions.Length == 1
               && request.Definitions[0].SourceName == "lower.js"
               && request.Options.Timeout == TimeSpan.FromSeconds(2),
            "script binding requests must defensively copy immutable definitions and options.");

        var result = JintBehaviorScriptProvider.BuildBindings(request);
        Assert(!result.Success
               && result.Bindings.Constraints.Single().BindingId == "binding:case-sensitive"
               && !result.Bindings.Constraints.Any(binding => binding.BindingId == "BINDING:case-sensitive"),
            "script binding id lookup must use exact ordinal equality.");
        Assert(result.Diagnostics.Single(diagnostic => diagnostic.BindingId == "BINDING:case-sensitive") is
               {
                   Code: "OMS1001",
                   Kind: BehaviorCatalogKind.Constraint,
                   Slot: BehaviorCatalogCallbackSlot.When,
               },
            "missing case-distinct binding ids must produce a structured diagnostic for the exact slot.");
        Assert(((ICollection<JintBehaviorScriptDiagnostic>)result.Diagnostics).IsReadOnly
               && RejectsImmutableArrayMutation(
                   result.Diagnostics,
                   result.Diagnostics[0] with { Message = "mutated" }),
            "script binding results and diagnostics must expose immutable read-only collections.");
    }

    private static void AssertExactCallbackSlotMapping()
    {
        var catalog = AllCallbackShapesCatalog();
        var result = JintBehaviorScriptProvider.BuildBindings(
            new JintBehaviorScriptBindingRequest(catalog, AllCallbackShapeDefinitions()));
        Assert(result.Success && result.Diagnostics.IsEmpty, "complete script definitions should bind without diagnostics.");
        Assert(result.Bindings.Constraints.Select(binding => binding.BindingId).SequenceEqual([
                "script:constraint:then",
                "script:constraint:when",
            ]),
            "Constraint When/Then must map to BehaviorConstraintCallbackBinding in stable binding-id order.");
        Assert(result.Bindings.Validators.Select(binding => binding.BindingId).SequenceEqual(["script:constraint:validator"]),
            "Constraint Validator must map to BehaviorValidatorCallbackBinding.");
        Assert(result.Bindings.ComputedProps.Select(binding => binding.BindingId).SequenceEqual(["script:computedProp"]),
            "ComputedProp Compute must map to BehaviorComputedPropCallbackBinding.");
        Assert(result.Bindings.Operations.Select(binding => binding.BindingId).SequenceEqual(["script:operation"]),
            "Operation Handler must map to BehaviorOperationCallbackBinding.");
        Assert(result.Bindings.Mutations.Select(binding => binding.BindingId).SequenceEqual(["script:mutation"]),
            "Mutation Executor must map to BehaviorMutationCallbackBinding.");
        Assert(result.Bindings.Interceptors.Select(binding => binding.BindingId).SequenceEqual(["script:interceptor"]),
            "Interceptor Handler must map to BehaviorInterceptorCallbackBinding.");
    }

    private static void AssertMissingAndConflictingDefinitionsAreDeterministic()
    {
        var missing = JintBehaviorScriptProvider.BuildBindings(new JintBehaviorScriptBindingRequest(
            new BehaviorCatalog([Operation("DiagnosticsOwner", "missing_operation", "missing:operation")]),
            []));
        Assert(!missing.Success
               && missing.Bindings.Operations.IsEmpty
               && missing.Diagnostics.SequenceEqual([
                   new JintBehaviorScriptDiagnostic(
                       "OMS1001",
                       "Script definition is missing for binding id 'missing:operation'.",
                       "missing:operation",
                       BehaviorCatalogKind.Operation,
                       "DiagnosticsOwner",
                       "missing_operation",
                       BehaviorCatalogCallbackSlot.Handler),
               ]),
            "missing script definitions must yield stable diagnostics and no binding for that slot.");

        var incompatible = JintBehaviorScriptProvider.BuildBindings(new JintBehaviorScriptBindingRequest(
            new BehaviorCatalog([
                Operation("DiagnosticsOwner", "shared_operation", "shared:id"),
                Interceptor("DiagnosticsOwner", "shared_operation", "before", 0, "shared:id"),
            ]),
            [new("shared:id", "() => null")]));
        AssertRejectedBeforeBindings(
            incompatible,
            "OMS1002",
            "shared:id",
            "one binding id reused across incompatible callback shapes must reject before returning bindings.");

        var duplicate = JintBehaviorScriptProvider.BuildBindings(new JintBehaviorScriptBindingRequest(
            new BehaviorCatalog([Operation("DiagnosticsOwner", "duplicate_operation", "duplicate:id")]),
            [
                new("duplicate:id", "() => []", "first.js"),
                new("duplicate:id", "() => []", "second.js"),
            ]));
        AssertRejectedBeforeBindings(
            duplicate,
            "OMS1003",
            "duplicate:id",
            "duplicate script ids must reject before returning bindings.");
    }

    private static async Task AssertCrossSlotReuseRejectsAndSameSlotReuseSharesAsync()
    {
        const string sharedConstraintId = "slot-reuse:constraint";
        var crossSlotCatalog = new BehaviorCatalog([
            new BehaviorCatalogEntry(
                BehaviorCatalogKind.Constraint,
                "SlotReuseOwner",
                "cross_slot_constraint",
                "conditional",
                "cross-slot constraint",
                null,
                null,
                null,
                [
                    new(BehaviorCatalogCallbackSlot.When, sharedConstraintId, BehaviorReadiness.Unresolved),
                    new(BehaviorCatalogCallbackSlot.Then, sharedConstraintId, BehaviorReadiness.Unresolved),
                ]),
        ]);
        var crossSlot = JintBehaviorScriptProvider.BuildBindings(
            new JintBehaviorScriptBindingRequest(
                crossSlotCatalog,
                [new(sharedConstraintId, "() => true", "cross-slot.js")]));
        var crossSlotDiagnostic = crossSlot.Diagnostics.Single();
        Assert(!crossSlot.Success
               && crossSlotDiagnostic is
               {
                   Code: "OMS1002",
                   BindingId: sharedConstraintId,
                   Kind: BehaviorCatalogKind.Constraint,
                   OwnerClass: "SlotReuseOwner",
                   BehaviorName: "cross_slot_constraint",
                   Slot: BehaviorCatalogCallbackSlot.When,
               }
               && crossSlotDiagnostic.Message.Contains("distinct callback slots", StringComparison.Ordinal)
               && !crossSlotDiagnostic.Message.Contains("incompatible callback shapes", StringComparison.Ordinal),
            "cross-slot reuse must fail deterministically at the first canonical slot with a distinct identity-ambiguity diagnostic.");
        AssertAllBindingsEmpty(
            crossSlot.Bindings,
            "cross-slot reuse must reject before callable preflight or callback construction.");

        using var db = new CozoDb(engine: "mem", path: "");
        var om = new CozoOm(db);
        await om.InitSchemaAsync();
        await om.DefineClassAsync("SlotReuseOwner", "slot reuse owner");
        await om.CreateObjectAsync("slot-reuse:1", "SlotReuseOwner", "slot reuse entity");
        var beforeCrossSlotImport = await CaptureProviderValidationStateAsync(om);
        var rejectedImport = await om.ImportBehaviorManifestJsonAsync(
            Encoding.UTF8.GetString(BehaviorManifestJsonCodec.Encode(crossSlotCatalog)),
            crossSlot.Bindings,
            new BehaviorImportOptions(RequireReady: true));
        var afterCrossSlotImport = await CaptureProviderValidationStateAsync(om);
        Assert(!rejectedImport.Applied
               && rejectedImport.Unresolved.Length == 2
               && rejectedImport.Unresolved.Select(item => item.Slot).SequenceEqual([
                   BehaviorCatalogCallbackSlot.When,
                   BehaviorCatalogCallbackSlot.Then,
               ]),
            "cross-slot reuse must remain unresolved at the canonical RequireReady boundary.");
        AssertProviderStateUnchanged(
            beforeCrossSlotImport,
            afterCrossSlotImport,
            "cross-slot provider rejection and strict import must publish zero OM effects.");

        const string sharedActionId = "slot-reuse:operation";
        var sameSlotCatalog = new BehaviorCatalog([
            Operation("SlotReuseOwner", "same_slot_first", sharedActionId),
            Operation("SlotReuseOwner", "same_slot_second", sharedActionId),
        ]);
        var sameSlot = JintBehaviorScriptProvider.BuildBindings(
            new JintBehaviorScriptBindingRequest(
                sameSlotCatalog,
                [new(sharedActionId, "() => []", "same-slot.js")]));
        Assert(sameSlot.Success
               && sameSlot.Diagnostics.IsEmpty
               && sameSlot.Bindings.Operations.Length == 1
               && sameSlot.Bindings.Operations[0].BindingId == sharedActionId,
            "same-id, same-shape, same-slot reuse must generate exactly one provider-neutral callback.");

        var readyImport = await om.ImportBehaviorManifestJsonAsync(
            Encoding.UTF8.GetString(BehaviorManifestJsonCodec.Encode(sameSlotCatalog)),
            sameSlot.Bindings,
            new BehaviorImportOptions(RequireReady: true));
        var importedActions = (await om.GetBehaviorCatalogAsync()).Behaviors
            .Where(entry => entry.Kind == BehaviorCatalogKind.Operation
                            && entry.OwnerClass == "SlotReuseOwner"
                            && entry.Name.StartsWith("same_slot_", StringComparison.Ordinal))
            .OrderBy(entry => entry.Name, StringComparer.Ordinal)
            .ToImmutableArray();
        Assert(readyImport.Applied
               && readyImport.Diagnostics.IsEmpty
               && readyImport.Unresolved.IsEmpty
               && importedActions.Length == 2
               && importedActions.SelectMany(entry => entry.Callbacks)
                   .All(callback => callback.BindingId == sharedActionId
                                    && callback.Slot == BehaviorCatalogCallbackSlot.Handler
                                    && callback.Readiness == BehaviorReadiness.Ready),
            "one same-slot callback must satisfy both canonical entries without duplicate binding collection entries.");

        await om.ExecuteOperationAsync("slot-reuse:1", "same_slot_first");
        await om.ExecuteOperationAsync("slot-reuse:1", "same_slot_second");
    }

    private static void AssertNativeAndScriptBindingsMergeDeterministically()
    {
        var nativeCallbacks = new BehaviorCallbackBindingSet(operations:
        [
            new("native:operation", (_, _) => ValueTask.FromResult<IReadOnlyList<MutationSpec>>([])),
        ]);
        var coexist = JintBehaviorScriptProvider.BuildBindings(new JintBehaviorScriptBindingRequest(
            new BehaviorCatalog([Operation("MergeOwner", "script_operation", "script:operation")]),
            [new("script:operation", "() => []")],
            nativeCallbacks: nativeCallbacks));
        Assert(coexist.Success
               && coexist.Bindings.Operations.Select(binding => binding.BindingId).SequenceEqual(["native:operation", "script:operation"]),
            "native and script callbacks with distinct ids must coexist in the returned provider-neutral binding set.");

        var conflict = JintBehaviorScriptProvider.BuildBindings(new JintBehaviorScriptBindingRequest(
            new BehaviorCatalog([Operation("MergeOwner", "script_operation", "script:operation")]),
            [new("script:operation", "() => []")],
            nativeCallbacks: new BehaviorCallbackBindingSet(operations:
            [
                new("script:operation", (_, _) => ValueTask.FromResult<IReadOnlyList<MutationSpec>>([])),
            ])));
        AssertRejectedBeforeBindings(
            conflict,
            "OMS1004",
            "script:operation",
            "same-id native/script merge conflicts must be deterministic diagnostics before any binding is returned.");
    }

    private static async Task AssertProviderValidationHasNoOmEffectsAsync()
    {
        using var db = new CozoDb(engine: "mem", path: "");
        var om = new CozoOm(db);
        await om.InitSchemaAsync();
        await om.DefineClassAsync("PureOwner", "Provider validation owner");
        await om.DefineOperationAsync("PureOwner", "pure_operation", "provider validation target");
        await BehaviorBindingLogic.PutAsync(
            om.Runtime,
            new BehaviorBindingRow(
                new BehaviorBindingKey(BehaviorKind.Operation, "PureOwner", "pure_operation", BehaviorCallbackSlot.Handler, "", -1),
                "pure:missing"));

        var catalog = await om.GetBehaviorCatalogAsync();
        var before = await CaptureProviderValidationStateAsync(om);
        var result = JintBehaviorScriptProvider.BuildBindings(new JintBehaviorScriptBindingRequest(catalog, []));
        var after = await CaptureProviderValidationStateAsync(om);
        Assert(!result.Success && result.Diagnostics.Single().BindingId == "pure:missing",
            "provider validation should report missing definitions without invoking import.");
        Assert(before.Catalog.SequenceEqual(after.Catalog)
               && before.BindingRows.SequenceEqual(after.BindingRows)
               && before.Readiness.SequenceEqual(after.Readiness)
               && ReferenceEquals(before.RegistrySnapshot, after.RegistrySnapshot),
            "provider validation must not change OM metadata, binding rows, registry snapshot or readiness.");
    }

    private static async Task AssertRequireReadyBindingsExecuteScriptsAsync()
    {
        using var db = new CozoDb(engine: "mem", path: "");
        var om = new CozoOm(db);
        await om.InitSchemaAsync();
        await om.DefineClassAsync("ScriptOwner", "script owner");
        await om.CreateObjectAsync("script:1", "ScriptOwner", "script entity");

        var catalog = AllCallbackShapesCatalog();
        var result = JintBehaviorScriptProvider.BuildBindings(
            new JintBehaviorScriptBindingRequest(catalog, ExecutableCallbackShapeDefinitions()));
        var import = await om.ImportBehaviorManifestJsonAsync(
            Encoding.UTF8.GetString(BehaviorManifestJsonCodec.Encode(catalog)),
            result.Bindings,
            new BehaviorImportOptions(RequireReady: true));
        Assert(result.Success
               && import.Applied
               && import.Diagnostics.IsEmpty
               && import.Unresolved.IsEmpty,
            "RequireReady import must accept a complete script catalog covering every callback slot.");

        var validationContext = new OmValidationContext(om.Runtime, "script:1", "ScriptOwner");
        var computedContext = new OmComputedPropContext(om.Runtime, "script:1", "ScriptOwner");
        var actionContext = new OmOperationContext(
            om.Runtime,
            "script:1",
            "ScriptOwner",
            "ScriptOwner",
            new Dictionary<string, object?> { ["source"] = "script-test" });

        Assert(await result.Bindings.Constraints.Single(binding => binding.BindingId == "script:constraint:when").Callback(validationContext),
            "constraint when script must execute and return bool.");
        Assert(await result.Bindings.Constraints.Single(binding => binding.BindingId == "script:constraint:then").Callback(validationContext),
            "constraint then script must execute and return bool.");
        Assert(await result.Bindings.Validators.Single().Callback(validationContext) is null,
            "constraint validator script must execute and return null|string.");

        var firstComputedProp = await result.Bindings.ComputedProps.Single().Callback(computedContext);
        var secondComputedProp = await result.Bindings.ComputedProps.Single().Callback(computedContext);
        Assert(firstComputedProp is IReadOnlyDictionary<string, object?> firstComputedObject
               && secondComputedProp is IReadOnlyDictionary<string, object?> secondComputedObject
               && Convert.ToDouble(firstComputedObject["score"]) == 42
               && Convert.ToDouble(firstComputedObject["count"]) == 1
               && Convert.ToDouble(secondComputedObject["count"]) == 1,
            "computedProp script must return a JSON-compatible value and use a fresh engine per invocation.");

        var actionMutations = await result.Bindings.Operations.Single().Callback(
            actionContext,
            new Dictionary<string, object?> { ["source"] = "script-test" });
        Assert(actionMutations.SequenceEqual([
                new MutationSpec(
                    "script_mutation",
                    new Dictionary<string, object?> { ["answer"] = 42d }),
            ], MutationSpecComparer.Instance),
            "operation script must return an ordered MutationSpec array with optional params.");

        await result.Bindings.Mutations.Single().Callback(
            new OmMutationContext(om.Runtime, "script:1", "ScriptOwner"),
            new Dictionary<string, object?> { ["ignored"] = true });
        await result.Bindings.Interceptors.Single().Callback(actionContext);
    }

    private static async Task AssertDirectInputPreflightIsStructuredAndEffectFreeAsync()
    {
        using var db = new CozoDb(engine: "mem", path: "");
        var om = new CozoOm(db);
        await om.InitSchemaAsync();
        await om.DefineClassAsync("PreflightOwner", "preflight owner");
        await om.DefineOperationAsync("PreflightOwner", "unchanged_operation", "preflight target");
        var before = await CaptureProviderValidationStateAsync(om);

        var result = JintBehaviorScriptProvider.BuildBindings(new JintBehaviorScriptBindingRequest(
            new BehaviorCatalog([
                new BehaviorCatalogEntry(
                    (BehaviorCatalogKind)999,
                    "PreflightOwner",
                    "invalid_kind",
                    null,
                    null,
                    "invalid kind",
                    null,
                    null,
                    [new(BehaviorCatalogCallbackSlot.Handler, "invalid:kind", BehaviorReadiness.Unresolved)]),
                new BehaviorCatalogEntry(
                    BehaviorCatalogKind.Operation,
                    "PreflightOwner",
                    "invalid_slot",
                    null,
                    null,
                    "invalid slot",
                    null,
                    null,
                    [new(BehaviorCatalogCallbackSlot.Compute, "invalid:slot", BehaviorReadiness.Unresolved)]),
                Operation("PreflightOwner", "blank_binding", " "),
                Operation("PreflightOwner", "null_binding", null!),
            ]),
            [
                new JintBehaviorScriptDefinition(null!, "() => true"),
                new JintBehaviorScriptDefinition("blank:source", " "),
                new JintBehaviorScriptDefinition("null:source", null!),
            ],
            new JintBehaviorScriptOptions(
                Timeout: TimeSpan.Zero,
                MaxStatements: 0,
                MaxRecursionDepth: -1,
                MemoryLimitBytes: 0)));
        var after = await CaptureProviderValidationStateAsync(om);

        Assert(!result.Success
               && result.Bindings.Constraints.IsEmpty
               && result.Bindings.Validators.IsEmpty
               && result.Bindings.ComputedProps.IsEmpty
               && result.Bindings.Operations.IsEmpty
               && result.Bindings.Mutations.IsEmpty
               && result.Bindings.Interceptors.IsEmpty
               && result.Diagnostics.Select(diagnostic => diagnostic.Code).SequenceEqual([
                   "OMS1005",
                   "OMS1006",
                   "OMS1006",
                   "OMS1007",
                   "OMS1007",
                   "OMS1008",
                   "OMS1009",
                   "OMS1010",
                   "OMS1010",
                   "OMS1010",
                   "OMS1010",
               ]),
            "invalid direct provider inputs must return stable structured diagnostics and zero bindings.");
        Assert(before.Catalog.SequenceEqual(after.Catalog)
               && before.BindingRows.SequenceEqual(after.BindingRows)
               && before.Readiness.SequenceEqual(after.Readiness)
               && ReferenceEquals(before.RegistrySnapshot, after.RegistrySnapshot),
            "invalid direct provider inputs must not change OM metadata, binding rows, registry snapshot or readiness.");
    }

    private static async Task AssertCallablePreflightRejectsInvalidSourcesBeforeReadyImportAsync()
    {
        using var db = new CozoDb(engine: "mem", path: "");
        var om = new CozoOm(db);
        await om.InitSchemaAsync();
        await om.DefineClassAsync("CallableOwner", "callable preflight owner");
        await om.DefineOperationAsync("CallableOwner", "syntax_operation", "syntax target");
        await om.DefineOperationAsync("CallableOwner", "non_callable_operation", "non-callable target");

        var syntaxCatalog = new BehaviorCatalog([Operation("CallableOwner", "syntax_operation", "script:syntax")]);
        var syntax = JintBehaviorScriptProvider.BuildBindings(new JintBehaviorScriptBindingRequest(
            syntaxCatalog,
            [new("script:syntax", "() => {", "syntax.js")]));
        AssertRejectedBeforeBindings(
            syntax,
            "OMS1013",
            "script:syntax",
            "syntax-invalid script source must reject before returning any generated callbacks.");
        Assert(!syntax.Diagnostics.Single(diagnostic => diagnostic.Code == "OMS1013").Message.Contains("() => {", StringComparison.Ordinal),
            "callable preflight diagnostics must not embed full source text.");
        var syntaxDiagnostic = syntax.Diagnostics.Single(diagnostic => diagnostic.Code == "OMS1013");
        Assert(syntaxDiagnostic.SourceName == "syntax.js"
               && syntaxDiagnostic.Phase == JintBehaviorScriptFailurePhase.Compile
               && syntaxDiagnostic.Line > 0
               && syntaxDiagnostic.Column > 0,
            "syntax preflight diagnostics must expose structured source identity and a positive Acornima location.");
        var syntaxImport = await om.ImportBehaviorManifestJsonAsync(
            Encoding.UTF8.GetString(BehaviorManifestJsonCodec.Encode(syntaxCatalog)),
            syntax.Bindings,
            new BehaviorImportOptions(RequireReady: true));
        Assert(!syntaxImport.Applied
               && syntaxImport.Unresolved.Single().BindingId == "script:syntax",
            "RequireReady import must not mark a syntax-invalid script binding ready.");

        var nonCallableCatalog = new BehaviorCatalog([Operation("CallableOwner", "non_callable_operation", "script:not-callable")]);
        var nonCallable = JintBehaviorScriptProvider.BuildBindings(new JintBehaviorScriptBindingRequest(
            nonCallableCatalog,
            [new("script:not-callable", "42", "not-callable.js")]));
        AssertRejectedBeforeBindings(
            nonCallable,
            "OMS1014",
            "script:not-callable",
            "non-callable script source must reject before returning any generated callbacks.");
        Assert(!nonCallable.Diagnostics.Single(diagnostic => diagnostic.Code == "OMS1014").Message.Contains("42", StringComparison.Ordinal),
            "non-callable diagnostics must not embed full source text.");
        var nonCallableImport = await om.ImportBehaviorManifestJsonAsync(
            Encoding.UTF8.GetString(BehaviorManifestJsonCodec.Encode(nonCallableCatalog)),
            nonCallable.Bindings,
            new BehaviorImportOptions(RequireReady: true));
        Assert(!nonCallableImport.Applied
               && nonCallableImport.Unresolved.Single().BindingId == "script:not-callable",
            "RequireReady import must not mark a non-callable script binding ready.");
    }

    private static async Task AssertCallablePreflightDoesNotExecuteFunctionBodyAsync()
    {
        using var db = new CozoDb(engine: "mem", path: "");
        var om = new CozoOm(db);
        await om.InitSchemaAsync();
        await om.DefineClassAsync("BodyOwner", "body preflight owner");
        await om.CreateObjectAsync("body:1", "BodyOwner", "body entity");

        var result = JintBehaviorScriptProvider.BuildBindings(new JintBehaviorScriptBindingRequest(
            new BehaviorCatalog([Operation("BodyOwner", "body_operation", "script:body")]),
            [new("script:body", "() => { throw new Error('body executed'); }", "body.js")]));
        Assert(result.Success
               && result.Bindings.Operations.Single().BindingId == "script:body",
            "callable preflight must accept a function whose body would throw if executed.");

        var actionContext = new OmOperationContext(
            om.Runtime,
            "body:1",
            "BodyOwner",
            "BodyOwner",
            new Dictionary<string, object?>());
        var exception = await ExpectScriptExceptionAsync(
            () => result.Bindings.Operations.Single().Callback(actionContext, new Dictionary<string, object?>()).AsTask(),
            "OMS2002",
            "script:body",
            BehaviorCatalogKind.Operation,
            BehaviorCatalogCallbackSlot.Handler,
            JintBehaviorScriptFailurePhase.Execution,
            "body.js",
            "() => { throw new Error('body executed'); }");
        Assert(!exception.Message.Contains("body executed", StringComparison.Ordinal)
               && !exception.ToString().Contains("body executed", StringComparison.Ordinal),
            "callback invocation must execute the body without exposing arbitrary thrown text publicly.");
    }

    private static async Task AssertCallablePreflightIsResourceBoundedAndEffectFreeAsync()
    {
        using var db = new CozoDb(engine: "mem", path: "");
        var om = new CozoOm(db);
        await om.InitSchemaAsync();
        await om.DefineClassAsync("BoundedPreflightOwner", "bounded preflight owner");
        await om.DefineOperationAsync("BoundedPreflightOwner", "unchanged_operation", "preflight target");
        var before = await CaptureProviderValidationStateAsync(om);

        const string timeoutSecret = "do-not-leak-preflight-timeout-source";
        var cases = new[]
        {
            new
            {
                Name = "timeout",
                BindingId = "preflight:limit:timeout",
                Source = $"(() => {{ const secret = '{timeoutSecret}'; while (true) {{}} return () => true; }})()",
                Options = new JintBehaviorScriptOptions(
                    Timeout: TimeSpan.FromMilliseconds(50),
                    MaxStatements: int.MaxValue,
                    MaxRecursionDepth: 128,
                    MemoryLimitBytes: 4 * 1024 * 1024),
            },
            new
            {
                Name = "statements",
                BindingId = "preflight:limit:statements",
                Source = "(() => { let n = 0; while (n < 1000000) { n++; } return () => true; })()",
                Options = new JintBehaviorScriptOptions(
                    Timeout: TimeSpan.FromSeconds(1),
                    MaxStatements: 32,
                    MaxRecursionDepth: 128,
                    MemoryLimitBytes: 4 * 1024 * 1024),
            },
            new
            {
                Name = "recursion",
                BindingId = "preflight:limit:recursion",
                Source = "(() => { function recurse(n) { return recurse(n + 1); } recurse(0); return () => true; })()",
                Options = new JintBehaviorScriptOptions(
                    Timeout: TimeSpan.FromSeconds(1),
                    MaxStatements: 10_000,
                    MaxRecursionDepth: 8,
                    MemoryLimitBytes: 4 * 1024 * 1024),
            },
            new
            {
                Name = "memory",
                BindingId = "preflight:limit:memory",
                Source = "(() => { const values = []; for (let i = 0; i < 100000; i++) values.push('xxxxxxxxxxxxxxxx'); return () => true; })()",
                Options = new JintBehaviorScriptOptions(
                    Timeout: TimeSpan.FromSeconds(1),
                    MaxStatements: int.MaxValue,
                    MaxRecursionDepth: 128,
                    MemoryLimitBytes: 64 * 1024),
            },
        };

        foreach (var testCase in cases)
        {
            var result = await WithOuterGuardAsync(
                () => Task.FromResult(JintBehaviorScriptProvider.BuildBindings(
                    new JintBehaviorScriptBindingRequest(
                        new BehaviorCatalog([
                            Operation("BoundedPreflightOwner", testCase.Name, testCase.BindingId),
                        ]),
                        [new(testCase.BindingId, testCase.Source, testCase.Name + "-preflight.js")],
                        testCase.Options))),
                $"{testCase.Name} factory source did not terminate within the outer BuildBindings guard.");
            AssertRejectedBeforeBindings(
                result,
                "OMS1013",
                testCase.BindingId,
                $"{testCase.Name} factory source must fail callable preflight with zero bindings.");
            var diagnostic = result.Diagnostics.Single(item => item.Code == "OMS1013");
            Assert(diagnostic.SourceName == testCase.Name + "-preflight.js"
                   && diagnostic.Phase == JintBehaviorScriptFailurePhase.Compile
                   && !diagnostic.Message.Contains(testCase.Source, StringComparison.Ordinal)
                   && !diagnostic.Message.Contains(timeoutSecret, StringComparison.Ordinal),
                $"{testCase.Name} callable preflight failure must be binding-aware and source-redacted.");
        }

        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();
        await WithOuterGuardAsync(
            () => ExpectOperationCanceledAsync(
                () => Task.Run(() =>
                {
                    JintBehaviorScriptProvider.BuildBindings(new JintBehaviorScriptBindingRequest(
                        new BehaviorCatalog([
                            Operation("BoundedPreflightOwner", "cancelled", "preflight:cancelled"),
                        ]),
                        [new("preflight:cancelled", "() => true", "cancelled-preflight.js")],
                        new JintBehaviorScriptOptions(CancellationToken: cancelled.Token)));
                }),
                "caller cancellation during callable preflight must remain OperationCanceledException semantics."),
            "cancelled callable preflight did not terminate within the outer BuildBindings guard.");

        var after = await CaptureProviderValidationStateAsync(om);
        Assert(before.Catalog.SequenceEqual(after.Catalog)
               && before.BindingRows.SequenceEqual(after.BindingRows)
               && before.Readiness.SequenceEqual(after.Readiness)
               && ReferenceEquals(before.RegistrySnapshot, after.RegistrySnapshot),
            "resource-limited or cancelled callable preflight must not publish OM effects.");
    }

    private static void AssertRecordCopyAndNullElementsReceiveDiagnosticsInsteadOfExceptions()
    {
        var validRequest = new JintBehaviorScriptBindingRequest(
            new BehaviorCatalog([Operation("GraphOwner", "valid_operation", "graph:valid")]),
            [new("graph:valid", "() => []")]);
        var nullMembers = JintBehaviorScriptProvider.BuildBindings(validRequest with
        {
            Catalog = null!,
            Options = null!,
            NativeCallbacks = null!,
            Definitions = default,
        });
        Assert(nullMembers.Bindings.Operations.IsEmpty
               && nullMembers.Diagnostics.Select(diagnostic => diagnostic.Code).SequenceEqual([
                   "OMS1011",
                   "OMS1011",
                   "OMS1011",
                   "OMS1011",
               ]),
            "record-copy null request members must return graph diagnostics instead of raw exceptions.");

        var defaultCatalog = JintBehaviorScriptProvider.BuildBindings(validRequest with
        {
            Catalog = new BehaviorCatalog([]) with { Behaviors = default },
        });
        Assert(defaultCatalog.Bindings.Operations.IsEmpty
               && defaultCatalog.Diagnostics.Single().Code == "OMS1011",
            "record-copy default catalog behavior collection must be diagnosed before enumeration.");

        var nullElements = JintBehaviorScriptProvider.BuildBindings(new JintBehaviorScriptBindingRequest(
            new BehaviorCatalog([
                null!,
                Operation("GraphOwner", "default_callbacks", "graph:default") with { Callbacks = default },
                Operation("GraphOwner", "null_callback", "graph:null-callback") with
                {
                    Callbacks = new BehaviorCallbackBinding[] { null! }.ToImmutableArray(),
                },
            ]),
            new JintBehaviorScriptDefinition[] { null!, new("graph:default", "() => []") },
            nativeCallbacks: new BehaviorCallbackBindingSet(operations: new BehaviorOperationCallbackBinding[] { null! })));
        Assert(nullElements.Bindings.Operations.IsEmpty
               && nullElements.Diagnostics.Select(diagnostic => diagnostic.Code).SequenceEqual([
                   "OMS1011",
                   "OMS1012",
                   "OMS1012",
                   "OMS1012",
                   "OMS1012",
               ]),
            "null definition, catalog behavior/callback and native callback elements must return stable diagnostics.");
    }

    private static void AssertStableOrderingAcrossReversedInputs()
    {
        var catalog = AllCallbackShapesCatalog();
        var reversedCatalog = ReverseCatalog(catalog);
        var result = JintBehaviorScriptProvider.BuildBindings(
            new JintBehaviorScriptBindingRequest(catalog, ExecutableCallbackShapeDefinitions()));
        var reversed = JintBehaviorScriptProvider.BuildBindings(
            new JintBehaviorScriptBindingRequest(reversedCatalog, ExecutableCallbackShapeDefinitions().Reverse()));
        Assert(BindingIdSequences(result).SequenceEqual(BindingIdSequences(reversed)),
            "typed binding id sequences must be stable when catalog and definition order are reversed.");

        var missing = JintBehaviorScriptProvider.BuildBindings(new JintBehaviorScriptBindingRequest(catalog, []));
        var reversedMissing = JintBehaviorScriptProvider.BuildBindings(new JintBehaviorScriptBindingRequest(reversedCatalog, []));
        Assert(missing.Diagnostics.SequenceEqual(reversedMissing.Diagnostics),
            "diagnostic sequences must be stable when catalog order is reversed.");
    }

    private static async Task AssertP2CallbackResultShapesRemainExactAsync()
    {
        using var db = new CozoDb(engine: "mem", path: "");
        var contexts = await CreateP2CallbackContextsAsync(db);
        var result = JintBehaviorScriptProvider.BuildBindings(
            new JintBehaviorScriptBindingRequest(AllCallbackShapesCatalog(), [
                new("script:constraint:when", "ctx => ctx.objectId === 'p2:1'"),
                new("script:constraint:then", "() => false"),
                new("script:constraint:validator", "() => 'validator says no'"),
                new("script:computedProp", "ctx => ({ objectId: ctx.objectId, nested: { score: 42 }, flags: [true, false] })"),
                new("script:operation", "(ctx, params) => [{ mutation: 'set_status', params: { status: params.status, rank: 7 } }, { mutation: 'audit' }]"),
                new("script:mutation", "() => ({ ignored: true })"),
                new("script:interceptor", "() => 'ignored'"),
            ]));

        Assert(result.Success && result.Diagnostics.IsEmpty, "P2 exact result-shape catalog should bind cleanly.");
        Assert(await result.Bindings.Constraints.Single(binding => binding.BindingId == "script:constraint:when").Callback(contexts.Validation),
            "constraint when must preserve exact boolean true.");
        Assert(!await result.Bindings.Constraints.Single(binding => binding.BindingId == "script:constraint:then").Callback(contexts.Validation),
            "constraint then must preserve exact boolean false.");
        Assert(await result.Bindings.Validators.Single().Callback(contexts.Validation) == "validator says no",
            "validator must preserve exact null|string result semantics.");

        var computedProp = await result.Bindings.ComputedProps.Single().Callback(contexts.ComputedProp);
        Assert(computedProp is IReadOnlyDictionary<string, object?> computedObject
               && Equals(computedObject["objectId"], "p2:1")
               && computedObject["nested"] is IReadOnlyDictionary<string, object?> nested
               && Convert.ToDouble(nested["score"]) == 42
               && computedObject["flags"] is IReadOnlyList<object?> flags
               && flags.Count == 2
               && Equals(flags[0], true)
               && Equals(flags[1], false),
            "computedProp callbacks must return only JSON-shaped values with nested object/array shape preserved.");

        var mutations = await result.Bindings.Operations.Single().Callback(
            contexts.Operation,
            new Dictionary<string, object?> { ["status"] = "ready" });
        var firstMutationParameters = mutations.ElementAtOrDefault(0)?.Params;
        Assert(mutations.Count == 2
               && mutations[0].Mutation == "set_status"
               && firstMutationParameters is not null
               && Equals(firstMutationParameters["status"], "ready")
               && Convert.ToDouble(firstMutationParameters["rank"]) == 7
               && mutations[1].Mutation == "audit"
               && mutations[1].Params is null,
            "operation callbacks must preserve ordered MutationSpec arrays with optional object params.");

        await result.Bindings.Mutations.Single().Callback(contexts.Mutation, new Dictionary<string, object?>());
        await result.Bindings.Interceptors.Single().Callback(contexts.Operation);
    }

    private static async Task AssertP2HostReadCapabilitiesAndAmbientDenialAsync()
    {
        using var db = new CozoDb(engine: "mem", path: "");
        var om = new CozoOm(db);
        await om.InitSchemaAsync();
        await om.DefineClassAsync("HostReadOwner", "script host read owner");
        await om.DefineFieldAsync("HostReadOwner", "name", OmValueType.String);
        await om.DefineRelationDefAsync("host_peer", "HostReadOwner", "HostReadOwner");
        await om.CreateObjectAsync("host:read:1", "HostReadOwner", "first");
        await om.CreateObjectAsync("host:read:2", "HostReadOwner", "second");
        await om.SetFieldValueAsync(
            "host:read:1",
            "name",
            "historical",
            new WriteOptions(ValidTime: "2024-01-01T00:00:00Z"));
        await om.SetFieldValueAsync(
            "host:read:1",
            "name",
            "current",
            new WriteOptions(ValidTime: "2025-01-01T00:00:00Z"));
        await om.CreateRelationLinkAsync(
            "host:read:1",
            "host_peer",
            "host:read:2",
            options: new WriteOptions(ValidTime: "2024-01-01T00:00:00Z"));

        const string validationSource = """
            async (identity, host) => {
              const pending = host.getFieldValue('name');
              const current = await pending;
              const historical = await host.getFieldValueAsOf('name', '2024-06-01T00:00:00Z');
              const neighbors = await host.getNeighbors('host_peer', 'outgoing');
              identity.objectId = 'tampered';
              return pending instanceof Promise
                && current === 'current'
                && historical === 'historical'
                && neighbors.outgoing.length === 1
                && neighbors.outgoing[0].objectId === 'host:read:2'
                && identity.objectId === 'host:read:1'
                && Object.isFrozen(identity)
                && Object.isFrozen(host)
                && Object.getPrototypeOf(host) === null
                && typeof host.setFieldValue === 'undefined'
                && typeof host.createRelationLink === 'undefined'
                && typeof host.callParentOperation === 'undefined'
                && typeof host.unknownCapability === 'undefined'
                && typeof host.getFieldValue.Method === 'undefined'
                && typeof host.getFieldValue.Target === 'undefined'
                && typeof identity.runtime === 'undefined'
                && typeof identity.Runtime === 'undefined'
                && typeof clr === 'undefined'
                && typeof System === 'undefined'
                && typeof require === 'undefined'
                && typeof importModule === 'undefined';
            }
            """;
        const string computedSource = """
            async (identity, host) => {
              const value = await host.getFieldValue('name');
              const neighbors = await host.getNeighbors('host_peer', 'outgoing');
              return {
                value,
                neighborCount: neighbors.outgoing.length,
                asOf: identity.asOf,
                explicitAsOfDenied: typeof host.getFieldValueAsOf === 'undefined',
                writeDenied: typeof host.setFieldValue === 'undefined'
              };
            }
            """;
        var result = JintBehaviorScriptProvider.BuildBindings(new JintBehaviorScriptBindingRequest(
            new BehaviorCatalog([
                Constraint("HostReadOwner", "host_validation", BehaviorCatalogCallbackSlot.When, "p2:host:validation"),
                ComputedProp("HostReadOwner", "host_computed_prop", "p2:host:computedProp"),
            ]),
            [
                new("p2:host:validation", validationSource, "host-validation.js"),
                new("p2:host:computedProp", computedSource, "host-computedProp.js"),
            ]));
        Assert(result.Success, "read-capability host scripts must bind cleanly.");

        var validation = await result.Bindings.Constraints.Single().Callback(
            new OmValidationContext(om.Runtime, "host:read:1", "HostReadOwner"));
        Assert(validation,
            "validation host must expose async current/as-of/neighbor reads while denying write, CLR and ambient capabilities.");

        var computedProp = await result.Bindings.ComputedProps.Single().Callback(
            new OmComputedPropContext(om.Runtime, "host:read:1", "HostReadOwner", "2024-06-01T00:00:00Z"));
        Assert(computedProp is IReadOnlyDictionary<string, object?> computedObject
               && Equals(computedObject["value"], "historical")
               && Convert.ToDouble(computedObject["neighborCount"]) == 1
               && Equals(computedObject["asOf"], "2024-06-01T00:00:00Z")
               && Equals(computedObject["explicitAsOfDenied"], true)
               && Equals(computedObject["writeDenied"], true),
            "computedProp host reads must honor context AsOf and expose no explicit temporal override or writes.");
    }

    private static async Task AssertP2HostWriteCapabilitiesAndParentCompositionAsync()
    {
        using var db = new CozoDb(engine: "mem", path: "");
        var om = new CozoOm(db);
        await om.InitSchemaAsync();
        await om.DefineClassAsync("HostWriteBase", "script host write base");
        await om.DefineClassAsync("HostWriteChild", "script host write child", parentClass: "HostWriteBase");
        await om.DefineFieldAsync("HostWriteBase", "status", OmValueType.String);
        await om.DefineRelationDefAsync("host_link", "HostWriteBase", "HostWriteBase");
        await om.CreateObjectAsync("host:write:1", "HostWriteChild", "first");
        await om.CreateObjectAsync("host:write:2", "HostWriteBase", "second");
        await om.SetFieldValueAsync("host:write:1", "status", "initial");
        await om.DefineMutationAsync(
            "HostWriteBase",
            "parent_status",
            async (context, parameters) => await context.SetFieldValueAsync("status", parameters["value"]),
            "parent mutation returned by parent operation");
        await om.DefineOperationAsync(
            "HostWriteBase",
            "host_operation",
            (_, parameters) => ValueTask.FromResult<IReadOnlyList<MutationSpec>>([
                new("parent_status", new Dictionary<string, object?> { ["value"] = parameters["value"] }),
            ]),
            "parent operation");
        await om.DefineOperationAsync("HostWriteChild", "host_operation", "child operation metadata");

        const string mutationSource = """
            async (identity, params, host) => {
              params.status = 'tampered';
              const before = await host.getFieldValue('status');
              if (typeof host.getFieldValueAsOf !== 'function' || typeof host.getNeighbors !== 'function') throw new Error('mutation read capability missing');
              const writePending = host.setFieldValue('status', params.requested);
              if (!(writePending instanceof Promise)) throw new Error('setFieldValue must return Promise');
              await writePending;
              await host.createRelationLink('host_link', params.toId, { source: 'script-mutation' });
              if (typeof host.callParentOperation !== 'undefined') throw new Error('mutation parent capability leak');
              return { before, frozenParams: Object.isFrozen(params), requested: params.requested };
            }
            """;
        const string actionSource = """
            async (identity, params, host) => {
              const before = await host.getFieldValue('status');
              if (typeof host.getFieldValueAsOf !== 'function' || typeof host.getNeighbors !== 'function' || typeof host.createRelationLink !== 'function') throw new Error('operation capability missing');
              const parent = await host.callParentOperation('host_operation', { value: params.parentValue });
              const afterParent = await host.getFieldValue('status');
              await host.setFieldValue('status', params.actionValue);
              return parent.concat([{ mutation: 'child_observation', params: { before, afterParent } }]);
            }
            """;
        const string interceptorSource = """
            async (identity, host) => {
              if (typeof host.callParentOperation !== 'undefined') throw new Error('interceptor parent capability leak');
              if (typeof host.getFieldValueAsOf !== 'function' || typeof host.getNeighbors !== 'function') throw new Error('interceptor read capability missing');
              await host.setFieldValue('status', 'interceptor');
              await host.createRelationLink('host_link', 'host:write:2', { source: 'script-interceptor' });
            }
            """;
        var result = JintBehaviorScriptProvider.BuildBindings(new JintBehaviorScriptBindingRequest(
            new BehaviorCatalog([
                Mutation("HostWriteChild", "host_mutation", "p2:host:mutation"),
                Operation("HostWriteChild", "host_operation", "p2:host:operation"),
                Interceptor("HostWriteChild", "host_operation", "after", 0, "p2:host:interceptor"),
            ]),
            [
                new("p2:host:mutation", mutationSource, "host-mutation.js"),
                new("p2:host:operation", actionSource, "host-operation.js"),
                new("p2:host:interceptor", interceptorSource, "host-interceptor.js"),
            ]));
        Assert(result.Success, "write-capability host scripts must bind cleanly.");

        var mutationParameters = new Dictionary<string, object?>
        {
            ["requested"] = "mutation",
            ["toId"] = "host:write:2",
        };
        await result.Bindings.Mutations.Single().Callback(
            new OmMutationContext(om.Runtime, "host:write:1", "HostWriteChild"),
            mutationParameters);
        Assert(mutationParameters["requested"] as string == "mutation"
               && AsJsonString(await om.GetFieldValueAsync("host:write:1", "status")) == "mutation"
               && (await om.GetNeighborsAsync("host:write:1", "host_link", OmDirection.Outgoing)).Outgoing.Count == 1,
            "mutation host writes and links must execute through the existing OmMutationContext while input params stay isolated.");

        var actionContext = new OmOperationContext(
            om.Runtime,
            "host:write:1",
            "HostWriteChild",
            "HostWriteChild",
            new Dictionary<string, object?>());
        var actionMutations = await result.Bindings.Operations.Single().Callback(
            actionContext,
            new Dictionary<string, object?>
            {
                ["parentValue"] = "parent",
                ["actionValue"] = "operation",
            });
        var parentParameters = actionMutations.ElementAtOrDefault(0)?.Params;
        var observationParameters = actionMutations.ElementAtOrDefault(1)?.Params;
        Assert(actionMutations.Count == 2
               && actionMutations[0].Mutation == "parent_status"
               && parentParameters is not null
               && AsJsonStringValue(parentParameters["value"]) == "parent"
               && actionMutations[1].Mutation == "child_observation"
               && observationParameters is not null
               && AsJsonStringValue(observationParameters["before"]) == "mutation"
               && AsJsonStringValue(observationParameters["afterParent"]) == "mutation"
               && AsJsonString(await om.GetFieldValueAsync("host:write:1", "status")) == "operation",
            "operation parent dispatch must return JSON mutation specs without pre-applying them, while direct writes use OmOperationContext.");

        await result.Bindings.Interceptors.Single().Callback(actionContext);
        Assert(AsJsonString(await om.GetFieldValueAsync("host:write:1", "status")) == "interceptor",
            "interceptor host effects must execute through the existing operation-derived mutation context without parent dispatch.");
    }

    private static async Task AssertP2HostFailuresAreStructuredAndRedactedAsync()
    {
        using var db = new CozoDb(engine: "mem", path: "");
        var contexts = await CreateP2CallbackContextsAsync(db);
        const string source = "async (identity, host) => { const hostSecret = 'do-not-leak-host-source'; await host.getNeighbors(null, 'sideways'); return true; }";
        var result = JintBehaviorScriptProvider.BuildBindings(new JintBehaviorScriptBindingRequest(
            new BehaviorCatalog([
                Constraint("ScriptOwner", "host_failure", BehaviorCatalogCallbackSlot.When, "p2:host:failure"),
            ]),
            [new("p2:host:failure", source, "host-failure.js")]));
        Assert(result.Success, "host failure script must bind before invocation.");
        await ExpectScriptExceptionAsync(
            () => result.Bindings.Constraints.Single().Callback(contexts.Validation).AsTask(),
            "OMS2004",
            "p2:host:failure",
            BehaviorCatalogKind.Constraint,
            BehaviorCatalogCallbackSlot.When,
            JintBehaviorScriptFailurePhase.HostInvocation,
            "host-failure.js",
            source);

        const string secretAttribute = "host-secret-attribute-do-not-leak";
        var omFailureSource = $"async (identity, params, host) => {{ await host.setFieldValue('{secretAttribute}', 'value'); }}";
        var omFailure = JintBehaviorScriptProvider.BuildBindings(new JintBehaviorScriptBindingRequest(
            new BehaviorCatalog([Mutation("ScriptOwner", "host_om_failure", "p2:host:om-failure")]),
            [new("p2:host:om-failure", omFailureSource, "host-om-failure.js")]));
        Assert(omFailure.Success, "OM host failure script must bind before invocation.");
        var exception = await ExpectScriptExceptionAsync(
            () => omFailure.Bindings.Mutations.Single().Callback(
                contexts.Mutation,
                new Dictionary<string, object?>()).AsTask(),
            "OMS2004",
            "p2:host:om-failure",
            BehaviorCatalogKind.Mutation,
            BehaviorCatalogCallbackSlot.Executor,
            JintBehaviorScriptFailurePhase.HostInvocation,
            "host-om-failure.js",
            omFailureSource);
        Assert(!exception.Message.Contains(secretAttribute, StringComparison.Ordinal)
               && !exception.ToString().Contains(secretAttribute, StringComparison.Ordinal),
            "host exception details must not leak OM argument or storage error secrets.");

        const string bypassSource = "async (identity, params, host) => { await host.setFieldValue('status', 'bypass', { skipConstraints: true }); }";
        var bypass = JintBehaviorScriptProvider.BuildBindings(new JintBehaviorScriptBindingRequest(
            new BehaviorCatalog([Mutation("ScriptOwner", "host_bypass", "p2:host:bypass")]),
            [new("p2:host:bypass", bypassSource, "host-bypass.js")]));
        Assert(bypass.Success, "constraint-bypass probe must bind before host option validation.");
        await ExpectScriptExceptionAsync(
            () => bypass.Bindings.Mutations.Single().Callback(
                contexts.Mutation,
                new Dictionary<string, object?>()).AsTask(),
            "OMS2004",
            "p2:host:bypass",
            BehaviorCatalogKind.Mutation,
            BehaviorCatalogCallbackSlot.Executor,
            JintBehaviorScriptFailurePhase.HostInvocation,
            "host-bypass.js",
            bypassSource);
    }

    private static async Task AssertP2HostCancellationDoesNotPublishLateWritesAsync()
    {
        using var db = new CozoDb(engine: "mem", path: "");
        var om = new CozoOm(db);
        await om.InitSchemaAsync();
        await om.DefineClassAsync("HostCancelOwner", "script host cancellation owner");
        await om.DefineFieldAsync("HostCancelOwner", "status", OmValueType.String);
        await om.DefineConstraintAsync("HostCancelOwner", "pause_write", "custom", "pause writes for cancellation test");
        await om.CreateObjectAsync("host:cancel:1", "HostCancelOwner", "cancel target");
        await om.SetFieldValueAsync(
            "host:cancel:1",
            "status",
            "before",
            new WriteOptions(SkipConstraints: true));

        var validatorStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseValidator = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var validatorCompleted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        om.RegisterValidator("HostCancelOwner", "pause_write", async _ =>
        {
            validatorStarted.TrySetResult();
            await releaseValidator.Task.ConfigureAwait(false);
            validatorCompleted.TrySetResult();
            return null;
        });

        using var cancellation = new CancellationTokenSource();
        const string source = "async (identity, params, host) => { await host.setFieldValue('status', params.value); }";
        var result = JintBehaviorScriptProvider.BuildBindings(new JintBehaviorScriptBindingRequest(
            new BehaviorCatalog([Mutation("HostCancelOwner", "cancel_write", "p2:host:cancel")]),
            [new("p2:host:cancel", source, "host-cancel.js")],
            new JintBehaviorScriptOptions(
                Timeout: TimeSpan.FromSeconds(5),
                MaxStatements: 10_000,
                MaxRecursionDepth: 64,
                MemoryLimitBytes: 4 * 1024 * 1024,
                CancellationToken: cancellation.Token)));
        Assert(result.Success, "host cancellation script must bind cleanly.");

        var invocation = result.Bindings.Mutations.Single().Callback(
            new OmMutationContext(om.Runtime, "host:cancel:1", "HostCancelOwner"),
            new Dictionary<string, object?> { ["value"] = "late" }).AsTask();
        await WithOuterGuardAsync(
            () => validatorStarted.Task,
            "host write did not reach the cancellable OM validation path.");
        cancellation.Cancel();
        await ExpectOperationCanceledAsync(
            () => invocation,
            "cancellation during async host work must retain OperationCanceledException semantics.");
        releaseValidator.TrySetResult();
        await WithOuterGuardAsync(
            () => validatorCompleted.Task,
            "cancelled host write did not unwind its in-flight validator.");
        await Task.Delay(50);
        Assert(AsJsonString(await om.GetFieldValueAsync("host:cancel:1", "status")) == "before",
            "cancellation forwarded into the OM write path must roll back and prevent a late property effect.");
    }

    private static async Task AssertP2InvalidResultMatrixUsesStructuredScriptExceptionsAsync()
    {
        using var db = new CozoDb(engine: "mem", path: "");
        var contexts = await CreateP2CallbackContextsAsync(db);
        var cases = new InvalidResultCase[]
        {
            new(
                "when non-bool",
                Constraint("ScriptOwner", "bad_when", BehaviorCatalogCallbackSlot.When, "p2:bad:when"),
                "p2:bad:when",
                "() => 'true'",
                BehaviorCatalogKind.Constraint,
                BehaviorCatalogCallbackSlot.When,
                (bindings, ctx) => bindings.Bindings.Constraints.Single().Callback(ctx.Validation).AsTask()),
            new(
                "then non-bool",
                Constraint("ScriptOwner", "bad_then", BehaviorCatalogCallbackSlot.Then, "p2:bad:then"),
                "p2:bad:then",
                "() => 1",
                BehaviorCatalogKind.Constraint,
                BehaviorCatalogCallbackSlot.Then,
                (bindings, ctx) => bindings.Bindings.Constraints.Single().Callback(ctx.Validation).AsTask()),
            new(
                "validator non-null/string",
                Constraint("ScriptOwner", "bad_validator", BehaviorCatalogCallbackSlot.Validator, "p2:bad:validator"),
                "p2:bad:validator",
                "() => ({ message: 'not a string' })",
                BehaviorCatalogKind.Constraint,
                BehaviorCatalogCallbackSlot.Validator,
                (bindings, ctx) => bindings.Bindings.Validators.Single().Callback(ctx.Validation).AsTask()),
            new(
                "computedProp Date",
                ComputedProp("ScriptOwner", "bad_date", "p2:bad:computedProp:date"),
                "p2:bad:computedProp:date",
                "() => new Date(0)",
                BehaviorCatalogKind.ComputedProp,
                BehaviorCatalogCallbackSlot.Compute,
                (bindings, ctx) => bindings.Bindings.ComputedProps.Single().Callback(ctx.ComputedProp).AsTask()),
            new(
                "computedProp function",
                ComputedProp("ScriptOwner", "bad_function", "p2:bad:computedProp:function"),
                "p2:bad:computedProp:function",
                "() => function notJson() { return 1; }",
                BehaviorCatalogKind.ComputedProp,
                BehaviorCatalogCallbackSlot.Compute,
                (bindings, ctx) => bindings.Bindings.ComputedProps.Single().Callback(ctx.ComputedProp).AsTask()),
            new(
                "computedProp Map",
                ComputedProp("ScriptOwner", "bad_map", "p2:bad:computedProp:map"),
                "p2:bad:computedProp:map",
                "() => new Map([['key', 'value']])",
                BehaviorCatalogKind.ComputedProp,
                BehaviorCatalogCallbackSlot.Compute,
                (bindings, ctx) => bindings.Bindings.ComputedProps.Single().Callback(ctx.ComputedProp).AsTask()),
            new(
                "operation non-array",
                Operation("ScriptOwner", "bad_operation_non_array", "p2:bad:operation:non-array"),
                "p2:bad:operation:non-array",
                "() => ({ mutation: 'x' })",
                BehaviorCatalogKind.Operation,
                BehaviorCatalogCallbackSlot.Handler,
                (bindings, ctx) => bindings.Bindings.Operations.Single().Callback(ctx.Operation, new Dictionary<string, object?>()).AsTask()),
            new(
                "operation non-object mutation spec",
                Operation("ScriptOwner", "bad_operation_non_object", "p2:bad:operation:non-object"),
                "p2:bad:operation:non-object",
                "() => [1]",
                BehaviorCatalogKind.Operation,
                BehaviorCatalogCallbackSlot.Handler,
                (bindings, ctx) => bindings.Bindings.Operations.Single().Callback(ctx.Operation, new Dictionary<string, object?>()).AsTask()),
            new(
                "operation blank mutation",
                Operation("ScriptOwner", "bad_operation_blank", "p2:bad:operation:blank-mutation"),
                "p2:bad:operation:blank-mutation",
                "() => [{ mutation: '   ' }]",
                BehaviorCatalogKind.Operation,
                BehaviorCatalogCallbackSlot.Handler,
                (bindings, ctx) => bindings.Bindings.Operations.Single().Callback(ctx.Operation, new Dictionary<string, object?>()).AsTask()),
            new(
                "operation bad params",
                Operation("ScriptOwner", "bad_operation_params", "p2:bad:operation:params"),
                "p2:bad:operation:params",
                "() => [{ mutation: 'x', params: 42 }]",
                BehaviorCatalogKind.Operation,
                BehaviorCatalogCallbackSlot.Handler,
                (bindings, ctx) => bindings.Bindings.Operations.Single().Callback(ctx.Operation, new Dictionary<string, object?>()).AsTask()),
        };

        foreach (var testCase in cases)
        {
            var result = JintBehaviorScriptProvider.BuildBindings(new JintBehaviorScriptBindingRequest(
                new BehaviorCatalog([testCase.Entry]),
                [new(testCase.BindingId, testCase.Source, testCase.Name + ".js")]));
            Assert(result.Success, $"{testCase.Name} must reach invocation so the P2 conversion contract is exercised.");
            await ExpectScriptExceptionAsync(
                () => testCase.Invoke(result, contexts),
                "OMS2003",
                testCase.BindingId,
                testCase.Kind,
                testCase.Slot,
                JintBehaviorScriptFailurePhase.ResultConversion,
                testCase.Name + ".js",
                testCase.Source);
        }
    }

    private static async Task AssertP2RuntimeAndConversionFailuresAreSourceRedactedAsync()
    {
        using var db = new CozoDb(engine: "mem", path: "");
        var contexts = await CreateP2CallbackContextsAsync(db);
        const string runtimeSecret = "do-not-leak-runtime-thrown-secret";
        const string runtimeSource = """
            ctx => {
              const fullSourceSecret = 'do-not-leak-runtime-thrown-secret';
              throw new Error(fullSourceSecret);
            }
            """;
        var runtime = JintBehaviorScriptProvider.BuildBindings(new JintBehaviorScriptBindingRequest(
            new BehaviorCatalog([ComputedProp("ScriptOwner", "runtime_error", "p2:error:runtime")]),
            [new("p2:error:runtime", runtimeSource, "runtime-error.js")]));
        Assert(runtime.Success, "runtime failure test must bind before invocation.");
        var runtimeException = await ExpectScriptExceptionAsync(
            () => runtime.Bindings.ComputedProps.Single().Callback(contexts.ComputedProp).AsTask(),
            "OMS2002",
            "p2:error:runtime",
            BehaviorCatalogKind.ComputedProp,
            BehaviorCatalogCallbackSlot.Compute,
            JintBehaviorScriptFailurePhase.Execution,
            "runtime-error.js",
            runtimeSource);
        Assert(runtimeException.Line > 0 && runtimeException.Column > 0,
            "runtime JavaScript failures with known source locations must expose positive line and column values.");
        Assert(!runtimeException.Message.Contains(runtimeSecret, StringComparison.Ordinal)
               && !runtimeException.ToString().Contains(runtimeSecret, StringComparison.Ordinal),
            "public runtime exception text must not expose arbitrary text thrown by the script.");

        const string conversionSource = "ctx => ({ notJson: new Date(0), secret: 'do-not-leak-conversion-source' })";
        var conversion = JintBehaviorScriptProvider.BuildBindings(new JintBehaviorScriptBindingRequest(
            new BehaviorCatalog([ComputedProp("ScriptOwner", "conversion_error", "p2:error:conversion")]),
            [new("p2:error:conversion", conversionSource, "conversion-error.js")]));
        Assert(conversion.Success, "conversion failure test must bind before invocation.");
        await ExpectScriptExceptionAsync(
            () => conversion.Bindings.ComputedProps.Single().Callback(contexts.ComputedProp).AsTask(),
            "OMS2003",
            "p2:error:conversion",
            BehaviorCatalogKind.ComputedProp,
            BehaviorCatalogCallbackSlot.Compute,
            JintBehaviorScriptFailurePhase.ResultConversion,
            "conversion-error.js",
            conversionSource);
    }

    private static async Task AssertP2FiniteDefaultsBoundPendingPromisesAsync()
    {
        Assert(JintBehaviorScriptOptions.DefaultTimeout == TimeSpan.FromSeconds(2)
               && JintBehaviorScriptOptions.DefaultMaxStatements == 250_000
               && JintBehaviorScriptOptions.DefaultMaxRecursionDepth == 128
               && JintBehaviorScriptOptions.DefaultMemoryLimitBytes == 32L * 1024 * 1024,
            "adapter defaults must remain finite and documented as a stable contract.");

        using var db = new CozoDb(engine: "mem", path: "");
        var contexts = await CreateP2CallbackContextsAsync(db);
        const string source = "async () => await new Promise(() => {})";
        var result = JintBehaviorScriptProvider.BuildBindings(new JintBehaviorScriptBindingRequest(
            new BehaviorCatalog([ComputedProp("ScriptOwner", "default_timeout", "p2:limit:default-timeout")]),
            [new("p2:limit:default-timeout", source, "default-timeout.js")]));
        Assert(result.Success, "unset options must normalize to finite defaults and still bind.");

        await WithOuterGuardAsync(
            () => ExpectScriptExceptionAsync(
                () => result.Bindings.ComputedProps.Single().Callback(contexts.ComputedProp).AsTask(),
                "OMS2101",
                "p2:limit:default-timeout",
                BehaviorCatalogKind.ComputedProp,
                BehaviorCatalogCallbackSlot.Compute,
                JintBehaviorScriptFailurePhase.Timeout,
                "default-timeout.js",
                source),
            "a Promise that never settles must be stopped by the finite default wall-clock timeout.");
    }

    private static async Task AssertP2LimitsAreStableBindingAwareFailuresAsync()
    {
        using var db = new CozoDb(engine: "mem", path: "");
        var contexts = await CreateP2CallbackContextsAsync(db);
        var cases = new LimitCase[]
        {
            new(
                "timeout",
                "p2:limit:timeout",
                "async () => await new Promise(() => {})",
                new JintBehaviorScriptOptions(Timeout: TimeSpan.FromMilliseconds(50), MaxStatements: int.MaxValue, MaxRecursionDepth: 128, MemoryLimitBytes: 4 * 1024 * 1024),
                "OMS2101",
                JintBehaviorScriptFailurePhase.Timeout),
            new(
                "max statements",
                "p2:limit:statements",
                "() => { let n = 0; while (n < 1000000) { n++; } return n; }",
                new JintBehaviorScriptOptions(Timeout: TimeSpan.FromSeconds(5), MaxStatements: 32, MaxRecursionDepth: 128, MemoryLimitBytes: 4 * 1024 * 1024),
                "OMS2102",
                JintBehaviorScriptFailurePhase.Limit),
            new(
                "recursion",
                "p2:limit:recursion",
                "() => { function recurse(n) { return recurse(n + 1); } return recurse(0); }",
                new JintBehaviorScriptOptions(Timeout: TimeSpan.FromSeconds(5), MaxStatements: 10_000, MaxRecursionDepth: 8, MemoryLimitBytes: 4 * 1024 * 1024),
                "OMS2103",
                JintBehaviorScriptFailurePhase.Limit),
            new(
                "memory",
                "p2:limit:memory",
                "() => { const values = []; for (let i = 0; i < 100000; i++) values.push('xxxxxxxxxxxxxxxx'); return values; }",
                new JintBehaviorScriptOptions(Timeout: TimeSpan.FromSeconds(5), MaxStatements: int.MaxValue, MaxRecursionDepth: 128, MemoryLimitBytes: 64 * 1024),
                "OMS2104",
                JintBehaviorScriptFailurePhase.Limit),
        };

        foreach (var testCase in cases)
        {
            var result = JintBehaviorScriptProvider.BuildBindings(new JintBehaviorScriptBindingRequest(
                new BehaviorCatalog([ComputedProp("ScriptOwner", testCase.Name, testCase.BindingId)]),
                [new(testCase.BindingId, testCase.Source, testCase.Name + ".js")],
                testCase.Options));
            Assert(result.Success, $"{testCase.Name} limit case should bind and fail during invocation.");
            var repetitions = testCase.Code == "OMS2101" ? 3 : 1;
            for (var attempt = 0; attempt < repetitions; attempt++)
            {
                await WithOuterGuardAsync(
                    () => ExpectScriptExceptionAsync(
                        () => result.Bindings.ComputedProps.Single().Callback(contexts.ComputedProp).AsTask(),
                        testCase.Code,
                        testCase.BindingId,
                        BehaviorCatalogKind.ComputedProp,
                        BehaviorCatalogCallbackSlot.Compute,
                        testCase.Phase,
                        testCase.Name + ".js",
                        testCase.Source),
                    $"{testCase.Name} limit did not terminate within the outer guard on attempt {attempt + 1}.");
            }
        }
    }

    private static void AssertP2InvalidFiniteOptionsAreRejectedBeforeExecution()
    {
        var options = new[]
        {
            new JintBehaviorScriptOptions(Timeout: TimeSpan.Zero),
            new JintBehaviorScriptOptions(Timeout: TimeSpan.FromMilliseconds(-1)),
            new JintBehaviorScriptOptions(MaxStatements: 0),
            new JintBehaviorScriptOptions(MaxStatements: -1),
            new JintBehaviorScriptOptions(MaxStatements: (long)int.MaxValue + 1),
            new JintBehaviorScriptOptions(MaxRecursionDepth: 0),
            new JintBehaviorScriptOptions(MaxRecursionDepth: -1),
            new JintBehaviorScriptOptions(MemoryLimitBytes: 0),
            new JintBehaviorScriptOptions(MemoryLimitBytes: -1),
        };

        foreach (var option in options)
        {
            var result = JintBehaviorScriptProvider.BuildBindings(new JintBehaviorScriptBindingRequest(
                new BehaviorCatalog([ComputedProp("ScriptOwner", "option_reject", "p2:option:reject")]),
                [new("p2:option:reject", "() => { throw new Error('must not execute'); }")],
                option));
            AssertRejectedBeforeBindings(
                result,
                "OMS1010",
                "",
                "invalid finite Jint script options, including MaxStatements > int.MaxValue, must be rejected preflight.");
        }
    }

    private static async Task AssertP2CancellationUsesOperationCanceledSemanticsAsync()
    {
        using var db = new CozoDb(engine: "mem", path: "");
        var contexts = await CreateP2CallbackContextsAsync(db);

        using var alreadyCancelled = new CancellationTokenSource();
        var alreadyCancelledResult = JintBehaviorScriptProvider.BuildBindings(new JintBehaviorScriptBindingRequest(
            new BehaviorCatalog([ComputedProp("ScriptOwner", "already_cancelled", "p2:cancel:already")]),
            [new("p2:cancel:already", "() => 1", "already-cancelled.js")],
            new JintBehaviorScriptOptions(
                Timeout: TimeSpan.FromSeconds(1),
                MaxStatements: 10_000,
                MaxRecursionDepth: 64,
                MemoryLimitBytes: 4 * 1024 * 1024,
                CancellationToken: alreadyCancelled.Token)));
        Assert(alreadyCancelledResult.Success, "active cancellation options should build deterministic bindings.");
        alreadyCancelled.Cancel();
        await ExpectOperationCanceledAsync(
            () => alreadyCancelledResult.Bindings.ComputedProps.Single().Callback(contexts.ComputedProp).AsTask(),
            "already-cancelled script invocation must surface OperationCanceledException semantics.");

        using var duringExecution = new CancellationTokenSource();
        var duringExecutionResult = JintBehaviorScriptProvider.BuildBindings(new JintBehaviorScriptBindingRequest(
            new BehaviorCatalog([ComputedProp("ScriptOwner", "during_cancelled", "p2:cancel:during")]),
            [new("p2:cancel:during", "() => { while (true) {} }", "during-cancelled.js")],
            new JintBehaviorScriptOptions(
                Timeout: TimeSpan.FromSeconds(5),
                MaxStatements: 10_000_000,
                MaxRecursionDepth: 64,
                MemoryLimitBytes: 4 * 1024 * 1024,
                CancellationToken: duringExecution.Token)));
        Assert(duringExecutionResult.Success, "during-execution cancellation case should build deterministic bindings.");
        duringExecution.CancelAfter(TimeSpan.FromMilliseconds(50));
        await WithOuterGuardAsync(
            () => ExpectOperationCanceledAsync(
                () => duringExecutionResult.Bindings.ComputedProps.Single().Callback(contexts.ComputedProp).AsTask(),
                "cancellation during an infinite script must surface OperationCanceledException semantics, not script failure."),
            "during-execution cancellation did not terminate within the outer guard.");
    }

    private static async Task AssertP2ConcurrentInvocationsUseFreshEnginesAsync()
    {
        using var db = new CozoDb(engine: "mem", path: "");
        var contexts = await CreateP2CallbackContextsAsync(db);
        var result = JintBehaviorScriptProvider.BuildBindings(new JintBehaviorScriptBindingRequest(
            new BehaviorCatalog([ComputedProp("ScriptOwner", "concurrent_fresh", "p2:fresh:concurrent")]),
            [new("p2:fresh:concurrent", "() => { globalThis.counter = (globalThis.counter || 0) + 1; return { counter: globalThis.counter }; }")],
            new JintBehaviorScriptOptions(Timeout: TimeSpan.FromSeconds(1), MaxStatements: 1000, MaxRecursionDepth: 32, MemoryLimitBytes: 4 * 1024 * 1024)));
        Assert(result.Success, "fresh/concurrent isolation case should bind.");

        var values = await Task.WhenAll(Enumerable.Range(0, 32).Select(async _ =>
        {
            var value = await result.Bindings.ComputedProps.Single().Callback(contexts.ComputedProp);
            return value is IReadOnlyDictionary<string, object?> obj ? Convert.ToDouble(obj["counter"]) : -1;
        }));
        Assert(values.All(value => value == 1),
            "many parallel invocations must each see a fresh global object and complete without sharing a Jint Engine.");
    }

    private static void AssertP2ProviderDoesNotSynchronouslyBlockHostTasks()
    {
        var repoRoot = FindRepoRoot();
        var providerSource = File.ReadAllText(Path.Combine(
            repoRoot,
            "src",
            "Depa.Ontology.Scripting.Jint",
            "JintBehaviorScriptProvider.cs"));
        Assert(!providerSource.Contains(".Result", StringComparison.Ordinal)
               && !providerSource.Contains(".Wait(", StringComparison.Ordinal)
               && !providerSource.Contains(".GetAwaiter().GetResult(", StringComparison.Ordinal),
            "Jint host bridge must await tasks instead of using synchronous Task Result/Wait/GetResult.");
    }

    private static async Task AssertP3CanonicalManifestImportExecutesScriptAndNativeCallbacksAsync()
    {
        using var db = new CozoDb(engine: "mem", path: "");
        var om = await CreateP3OwnerAsync(db, "p3:canonical");
        var catalog = P3IntegrationCatalog();
        var canonicalJson = BehaviorManifestJsonCodec.Encode(catalog);
        var decoded = BehaviorManifestJsonCodec.Decode(canonicalJson);
        Assert(decoded.Success && decoded.Catalog is not null,
            "P3 canonical script catalog must survive JSON encode/decode before provider binding.");
        AssertCanonicalManifestHasNoScriptRuntimeSchema(canonicalJson, P3IntegrationDefinitions());

        var nativeCallbacks = P3NativeCallbacks();
        var provider = JintBehaviorScriptProvider.BuildBindings(new JintBehaviorScriptBindingRequest(
            decoded.Catalog!,
            P3IntegrationDefinitions(),
            nativeCallbacks: nativeCallbacks));
        Assert(provider.Success
               && provider.Bindings.ComputedProps.Select(binding => binding.BindingId).SequenceEqual([
                   "p3:native:computedProp",
                   "p3:script:computedProp",
               ]),
            "canonical provider binding must merge native and script callbacks into one provider-neutral binding set.");

        var import = await om.ImportBehaviorManifestJsonAsync(
            Encoding.UTF8.GetString(canonicalJson),
            provider.Bindings,
            new BehaviorImportOptions(RequireReady: true));
        Assert(import is { Applied: true, Diagnostics.Length: 0, Unresolved.Length: 0 },
            "canonical RequireReady import must publish every exact script/native binding id.");
        var imported = await om.GetBehaviorCatalogAsync();
        Assert(imported.Behaviors.Length == catalog.Behaviors.Length
               && imported.Behaviors.SelectMany(entry => entry.Callbacks)
                   .All(callback => callback.Readiness == BehaviorReadiness.Ready),
            "every callback shape in the canonical integration catalog must be Ready after strict import.");

        var validation = await om.ValidateObjectAsync("p3:canonical");
        Assert(validation.Valid,
            "strict canonical import must execute script conditional when/then and custom validator callbacks.");
        var validationContext = new OmValidationContext(om.Runtime, "p3:canonical", "P3ScriptOwner");
        Assert(await provider.Bindings.Constraints.Single(binding => binding.BindingId == "p3:script:constraint:when").Callback(validationContext)
               && await provider.Bindings.Constraints.Single(binding => binding.BindingId == "p3:script:constraint:then").Callback(validationContext)
               && await provider.Bindings.Validators.Single(binding => binding.BindingId == "p3:script:constraint:validator").Callback(validationContext) is null,
            "imported provider bindings must execute all three constraint callback shapes.");

        var scriptComputedProp = await om.GetFieldValueAsync("p3:canonical", "script_computed_prop");
        Assert(scriptComputedProp is { ValueKind: JsonValueKind.Object } computedProp
               && computedProp.GetProperty("engine").GetString() == "jint"
               && computedProp.GetProperty("objectId").GetString() == "p3:canonical",
            "canonical import must execute the script computedProp callback through the OM registry.");
        Assert(AsJsonString(await om.GetFieldValueAsync("p3:canonical", "native_computed_prop")) == "native-ready",
            "native typed callbacks must coexist with scripts in the same imported provider-neutral binding set.");

        await om.ExecuteOperationAsync(
            "p3:canonical",
            "script_operation",
            new Dictionary<string, object?> { ["value"] = "from-canonical-operation" });
        Assert(AsJsonString(await om.GetFieldValueAsync("p3:canonical", "effect")) == "from-canonical-operation"
               && AsJsonString(await om.GetFieldValueAsync("p3:canonical", "interceptor_marker")) == "after-script-operation",
            "imported script operation, returned mutation and interceptor must execute through the OM runtime.");

        await om.ExecuteMutationsAsync(
            "p3:canonical",
            [new MutationSpec("script_mutation", new Dictionary<string, object?> { ["value"] = "from-direct-mutation" })]);
        Assert(AsJsonString(await om.GetFieldValueAsync("p3:canonical", "effect")) == "from-direct-mutation",
            "the imported script mutation callback must also execute on the representative direct mutation path.");
    }

    private static async Task AssertP3RequireReadyFailuresAreAtomicAsync()
    {
        using var db = new CozoDb(engine: "mem", path: "");
        var om = new CozoOm(db);
        await om.InitSchemaAsync();
        await om.DefineClassAsync("P3AtomicOwner", "P3 strict import owner");
        var catalog = new BehaviorCatalog([
            ComputedProp("P3AtomicOwner", "missing_computed_prop", "p3:missing:computedProp"),
        ]);
        var json = Encoding.UTF8.GetString(BehaviorManifestJsonCodec.Encode(catalog));
        var before = await CaptureProviderValidationStateAsync(om);

        var defaultImport = await om.ImportBehaviorManifestJsonAsync(
            json,
            options: new BehaviorImportOptions(RequireReady: true));
        var afterDefault = await CaptureProviderValidationStateAsync(om);
        Assert(!defaultImport.Applied
               && defaultImport.Unresolved.Single() is
               {
                   BindingId: "p3:missing:computedProp",
                   Kind: BehaviorCatalogKind.ComputedProp,
                   Slot: BehaviorCatalogCallbackSlot.Compute,
               },
            "default strict import must leave a missing script callback unresolved.");
        AssertProviderStateUnchanged(before, afterDefault,
            "default RequireReady failure must publish zero metadata, binding, registry or readiness effects.");

        var missingProvider = JintBehaviorScriptProvider.BuildBindings(
            new JintBehaviorScriptBindingRequest(catalog, []));
        Assert(!missingProvider.Success
               && missingProvider.Diagnostics.Single() is
               {
                   Code: "OMS1001",
                   BindingId: "p3:missing:computedProp",
               }
               && missingProvider.Bindings.ComputedProps.IsEmpty,
            "provider diagnostics must report missing script source without manufacturing a ready callback.");
        var providerImport = await om.ImportBehaviorManifestJsonAsync(
            json,
            missingProvider.Bindings,
            new BehaviorImportOptions(RequireReady: true));
        var afterProvider = await CaptureProviderValidationStateAsync(om);
        Assert(!providerImport.Applied
               && providerImport.Unresolved.Single().BindingId == "p3:missing:computedProp",
            "provider diagnostics must not accidentally satisfy canonical import readiness.");
        AssertProviderStateUnchanged(before, afterProvider,
            "missing-script provider import must remain atomically effect-free.");

        var conflictProvider = JintBehaviorScriptProvider.BuildBindings(new JintBehaviorScriptBindingRequest(
            catalog,
            [new("p3:missing:computedProp", "() => 'script'")],
            nativeCallbacks: new BehaviorCallbackBindingSet(computedProp:
            [
                new("p3:missing:computedProp", _ => ValueTask.FromResult<object?>("native")),
            ])));
        AssertRejectedBeforeBindings(
            conflictProvider,
            "OMS1004",
            "p3:missing:computedProp",
            "same-id native/script conflict must deterministically return zero bindings.");
        var conflictImport = await om.ImportBehaviorManifestJsonAsync(
            json,
            conflictProvider.Bindings,
            new BehaviorImportOptions(RequireReady: true));
        var afterConflict = await CaptureProviderValidationStateAsync(om);
        Assert(!conflictImport.Applied
               && conflictImport.Unresolved.Single().BindingId == "p3:missing:computedProp",
            "same-id provider conflict must remain unresolved at the canonical import boundary.");
        AssertProviderStateUnchanged(before, afterConflict,
            "same-id native/script conflict must be effect-free across provider and import boundaries.");
    }

    private static async Task AssertP3RestartRequiresExactRebindWithoutPersistingSourcesAsync()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"cozo-om-jint-restart-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        var databasePath = Path.Combine(directory, "om.db");
        var canonicalJson = BehaviorManifestJsonCodec.Encode(P3IntegrationCatalog());
        var definitions = P3IntegrationDefinitions();
        try
        {
            using (var db = new CozoDb(engine: "sqlite", path: databasePath))
            {
                var om = await CreateP3OwnerAsync(db, "p3:restart");
                var decoded = BehaviorManifestJsonCodec.Decode(canonicalJson);
                var provider = JintBehaviorScriptProvider.BuildBindings(new JintBehaviorScriptBindingRequest(
                    decoded.Catalog!,
                    definitions,
                    nativeCallbacks: P3NativeCallbacks()));
                var import = await om.ImportBehaviorManifestJsonAsync(
                    Encoding.UTF8.GetString(canonicalJson),
                    provider.Bindings,
                    new BehaviorImportOptions(RequireReady: true));
                Assert(import.Applied
                       && (await om.GetBehaviorCatalogAsync()).Behaviors
                           .SelectMany(entry => entry.Callbacks)
                           .All(callback => callback.Readiness == BehaviorReadiness.Ready)
                       && AsJsonString(await om.GetFieldValueAsync("p3:restart", "native_computed_prop")) == "native-ready",
                    "initial persistent import must bind and execute both script and native callbacks.");
            }

            using (var reopenedDb = new CozoDb(engine: "sqlite", path: databasePath))
            {
                var reopened = new CozoOm(reopenedDb);
                await reopened.InitSchemaAsync();
                var unresolved = await reopened.GetBehaviorCatalogAsync();
                Assert(unresolved.Behaviors.Length == P3IntegrationCatalog().Behaviors.Length
                       && unresolved.Behaviors.SelectMany(entry => entry.Callbacks)
                           .All(callback => callback.Readiness == BehaviorReadiness.Unresolved),
                    "restart must preserve canonical behavior metadata while treating process-local callbacks as unresolved.");
                AssertCanonicalManifestHasNoScriptRuntimeSchema(
                    await reopened.ExportBehaviorManifestJsonAsync(),
                    definitions);
                await ExpectBehaviorUnresolvedAsync(
                    () => reopened.GetFieldValueAsync("p3:restart", "script_computed_prop"),
                    "restarted runtime must fail closed before script source is supplied again.");
                await ExpectBehaviorUnresolvedAsync(
                    () => reopened.GetFieldValueAsync("p3:restart", "native_computed_prop"),
                    "restarted runtime must fail closed before native callback is supplied again.");

                var decoded = BehaviorManifestJsonCodec.Decode(
                    await reopened.ExportBehaviorManifestJsonAsync());
                var reboundProvider = JintBehaviorScriptProvider.BuildBindings(new JintBehaviorScriptBindingRequest(
                    decoded.Catalog!,
                    definitions,
                    nativeCallbacks: P3NativeCallbacks()));
                var rebound = await reopened.ImportBehaviorManifestJsonAsync(
                    Encoding.UTF8.GetString(canonicalJson),
                    reboundProvider.Bindings,
                    new BehaviorImportOptions(RequireReady: true));
                Assert(rebound is { Applied: true, Diagnostics.Length: 0, Unresolved.Length: 0 }
                       && (await reopened.GetBehaviorCatalogAsync()).Behaviors
                           .SelectMany(entry => entry.Callbacks)
                           .All(callback => callback.Readiness == BehaviorReadiness.Ready),
                    "the same exact script/native binding ids must restore readiness after restart.");
                Assert(AsJsonString(await reopened.GetFieldValueAsync("p3:restart", "native_computed_prop")) == "native-ready"
                       && await reopened.GetFieldValueAsync("p3:restart", "script_computed_prop") is
                           { ValueKind: JsonValueKind.Object },
                    "exact rebind must make both provider kinds executable again without persisted script source.");
            }
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static async Task AssertP3SandboxAndResourceFailuresRemainStableAfterReadyImportAsync()
    {
        const string ambientSource = """
            () => ({
              clrAbsent: typeof clr === 'undefined',
              systemAbsent: typeof System === 'undefined',
              requireAbsent: typeof require === 'undefined',
              importModuleAbsent: typeof importModule === 'undefined'
            })
            """;
        using (var ambientDb = new CozoDb(engine: "mem", path: ""))
        {
            var ambientOm = await CreateReadyComputedScriptAsync(
                ambientDb,
                "p3:ambient",
                "p3:sandbox:ambient",
                ambientSource,
                new JintBehaviorScriptOptions());
            var value = await ambientOm.GetFieldValueAsync("p3:ambient", "probe");
            Assert(value is { ValueKind: JsonValueKind.Object } ambient
                   && ambient.EnumerateObject().All(property => property.Value.ValueKind == JsonValueKind.True),
                "canonical ready import must keep CLR/System/require/importModule ambient capabilities absent.");
        }

        var failures = new[]
        {
            new P3IntegratedFailureCase(
                "timeout",
                "p3:resource:timeout",
                "() => new Promise(() => {})",
                new JintBehaviorScriptOptions(
                    Timeout: TimeSpan.FromMilliseconds(50),
                    MaxStatements: int.MaxValue,
                    MaxRecursionDepth: 128,
                    MemoryLimitBytes: 4 * 1024 * 1024),
                "OMS2101",
                JintBehaviorScriptFailurePhase.Timeout),
            new P3IntegratedFailureCase(
                "statements",
                "p3:resource:statements",
                "() => { let n = 0; while (n < 1000000) { n++; } return n; }",
                new JintBehaviorScriptOptions(
                    Timeout: TimeSpan.FromSeconds(1),
                    MaxStatements: 32,
                    MaxRecursionDepth: 128,
                    MemoryLimitBytes: 4 * 1024 * 1024),
                "OMS2102",
                JintBehaviorScriptFailurePhase.Limit),
            new P3IntegratedFailureCase(
                "recursion",
                "p3:resource:recursion",
                "() => { function recurse(n) { return recurse(n + 1); } return recurse(0); }",
                new JintBehaviorScriptOptions(
                    Timeout: TimeSpan.FromSeconds(1),
                    MaxStatements: 10_000,
                    MaxRecursionDepth: 8,
                    MemoryLimitBytes: 4 * 1024 * 1024),
                "OMS2103",
                JintBehaviorScriptFailurePhase.Limit),
            new P3IntegratedFailureCase(
                "memory",
                "p3:resource:memory",
                "() => { const values = []; for (let i = 0; i < 100000; i++) values.push('xxxxxxxxxxxxxxxx'); return values.length; }",
                new JintBehaviorScriptOptions(
                    Timeout: TimeSpan.FromSeconds(1),
                    MaxStatements: int.MaxValue,
                    MaxRecursionDepth: 128,
                    MemoryLimitBytes: 64 * 1024),
                "OMS2104",
                JintBehaviorScriptFailurePhase.Limit),
        };

        foreach (var failure in failures)
        {
            using var db = new CozoDb(engine: "mem", path: "");
            var om = await CreateReadyComputedScriptAsync(
                db,
                "p3:" + failure.Name,
                failure.BindingId,
                failure.Source,
                failure.Options);
            await WithOuterGuardAsync(
                () => ExpectScriptExceptionAsync(
                    async () => { await om.GetFieldValueAsync("p3:" + failure.Name, "probe"); },
                    failure.Code,
                    failure.BindingId,
                    BehaviorCatalogKind.ComputedProp,
                    BehaviorCatalogCallbackSlot.Compute,
                    failure.Phase,
                    failure.Name + ".js",
                    failure.Source),
                $"{failure.Name} failure did not terminate after canonical ready import.");
            Assert((await om.GetBehaviorCatalogAsync()).Behaviors.Single().Callbacks.Single().Readiness
                   == BehaviorReadiness.Ready,
                $"{failure.Name} adapter failure must preserve canonical binding readiness.");
        }

        using var cancellation = new CancellationTokenSource();
        using var cancellationDb = new CozoDb(engine: "mem", path: "");
        var cancellationOm = await CreateReadyComputedScriptAsync(
            cancellationDb,
            "p3:cancellation",
            "p3:resource:cancellation",
            "() => 42",
            new JintBehaviorScriptOptions(CancellationToken: cancellation.Token));
        cancellation.Cancel();
        await WithOuterGuardAsync(
            () => ExpectOperationCanceledAsync(
                async () => { await cancellationOm.GetFieldValueAsync("p3:cancellation", "probe"); },
                "caller cancellation must remain OperationCanceledException after canonical ready import."),
            "cancellation did not terminate after canonical ready import.");
        Assert((await cancellationOm.GetBehaviorCatalogAsync()).Behaviors.Single().Callbacks.Single().Readiness
               == BehaviorReadiness.Ready,
            "caller cancellation must not corrupt canonical callback readiness.");
    }

    private static async Task AssertP3ParentInheritanceAndTransactionCompositionAsync()
    {
        using var db = new CozoDb(engine: "mem", path: "");
        var om = await CreateP3TransactionOwnerAsync(new CozoDbOmStore(db), "p3:tx:composition");
        var baseMutationRan = false;
        var catalog = new BehaviorCatalog([
            Operation("P3TxBase", "compose", "p3:tx:base-operation"),
            Operation("P3TxChild", "compose", "p3:tx:child-operation"),
            Mutation("P3TxBase", "shared_mutation", "p3:tx:base-mutation"),
            Mutation("P3TxChild", "shared_mutation", "p3:tx:child-mutation"),
            Mutation("P3TxChild", "child_mutation", "p3:tx:child-only-mutation"),
            Interceptor("P3TxBase", "compose", "before", 0, "p3:tx:base-before"),
            Interceptor("P3TxChild", "compose", "before", 0, "p3:tx:child-before"),
            Interceptor("P3TxBase", "compose", "after", 0, "p3:tx:base-after"),
            Interceptor("P3TxChild", "compose", "after", 0, "p3:tx:child-after"),
        ]);
        var definitions = ImmutableArray.Create(
            new JintBehaviorScriptDefinition(
                "p3:tx:base-operation",
                """
                async (identity, params, host) => {
                  const trace = await host.getFieldValue('trace');
                  await host.setFieldValue('trace', trace + ',base-operation');
                  return [{ mutation: 'shared_mutation', params: { value: params.parentValue } }];
                }
                """,
                "p3-tx-base-operation.js"),
            new JintBehaviorScriptDefinition(
                "p3:tx:child-operation",
                """
                async (identity, params, host) => {
                  let trace = await host.getFieldValue('trace');
                  await host.setFieldValue('trace', trace + ',child-operation-start');
                  const parent = await host.callParentOperation('compose', { parentValue: params.parentValue });
                  const parentEffectBeforeApply = await host.getFieldValue('mutation_effect');
                  if (parentEffectBeforeApply !== 'initial-mutation') {
                    throw new Error('parent mutation was pre-applied');
                  }
                  trace = await host.getFieldValue('trace');
                  await host.setFieldValue('trace', trace + ',child-operation-end');
                  await host.setFieldValue('direct_effect', params.directValue);
                  await host.createRelationLink('p3_tx_link', params.targetId, { source: 'child-operation' });
                  return parent.concat([
                    { mutation: 'child_mutation', params: { value: params.childValue } }
                  ]);
                }
                """,
                "p3-tx-child-operation.js"),
            new JintBehaviorScriptDefinition(
                "p3:tx:child-mutation",
                """
                async (identity, params, host) => {
                  const trace = await host.getFieldValue('trace');
                  await host.setFieldValue('trace', trace + ',child-nearest-mutation');
                  await host.setFieldValue('mutation_effect', params.value);
                }
                """,
                "p3-tx-child-mutation.js"),
            new JintBehaviorScriptDefinition(
                "p3:tx:child-before",
                """
                async (identity, host) => {
                  const trace = await host.getFieldValue('trace');
                  await host.setFieldValue('trace', trace + ',child-before');
                }
                """,
                "p3-tx-child-before.js"),
            new JintBehaviorScriptDefinition(
                "p3:tx:base-after",
                """
                async (identity, host) => {
                  const trace = await host.getFieldValue('trace');
                  await host.setFieldValue('trace', trace + ',base-after');
                }
                """,
                "p3-tx-base-after.js"));
        var nativeCallbacks = new BehaviorCallbackBindingSet(
            mutations:
            [
                new("p3:tx:base-mutation", async (context, parameters) =>
                {
                    baseMutationRan = true;
                    await context.SetFieldValueAsync("mutation_effect", parameters["value"]);
                }),
                new("p3:tx:child-only-mutation", async (context, parameters) =>
                {
                    await AppendP3TraceAsync(context, "child-mutation");
                    await context.SetFieldValueAsync("child_effect", parameters["value"]);
                }),
            ],
            interceptors:
            [
                new("p3:tx:base-before", context => AppendP3TraceAsync(context, "base-before")),
                new("p3:tx:child-after", context => AppendP3TraceAsync(context, "child-after")),
            ]);
        await ImportReadyP3CatalogAsync(om, catalog, definitions, nativeCallbacks);

        await WithOuterGuardAsync(
            () => om.ExecuteOperationAsync(
                "p3:tx:composition",
                "compose",
                new Dictionary<string, object?>
                {
                    ["parentValue"] = "nearest-child",
                    ["childValue"] = "child-applied",
                    ["directValue"] = "direct-applied",
                    ["targetId"] = "p3:tx:target",
                }),
            "script parent/inheritance composition did not terminate.");

        Assert(!baseMutationRan,
            "a parent operation's returned mutation must resolve against the child entity and select the nearest child mutation.");
        Assert(AsJsonString(await om.GetFieldValueAsync("p3:tx:composition", "mutation_effect")) == "nearest-child"
               && AsJsonString(await om.GetFieldValueAsync("p3:tx:composition", "child_effect")) == "child-applied"
               && AsJsonString(await om.GetFieldValueAsync("p3:tx:composition", "direct_effect")) == "direct-applied",
            "direct script writes and explicitly composed parent/child mutation specs must commit in one operation transaction.");
        Assert((await om.GetNeighborsAsync(
                   "p3:tx:composition",
                   "p3_tx_link",
                   OmDirection.Outgoing)).Outgoing.Single().ObjectId == "p3:tx:target",
            "direct script host createRelationLink must commit with the operation transaction.");
        Assert(
            AsJsonString(await om.GetFieldValueAsync("p3:tx:composition", "trace")) ==
            "initial,base-before,child-before,child-operation-start,base-operation,child-operation-end,"
            + "child-nearest-mutation,child-mutation,base-after,child-after",
            "inherited interceptors, child override, parent operation and nearest mutations must preserve the existing deterministic C# order.");
    }

    private static async Task AssertOuterGuardPreservesCompletionAndTimeoutSemanticsAsync()
    {
        var completed = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        completed.SetResult("completed");
        Assert(await WithOuterGuardAsync(() => completed.Task, "completed work must not time out.") == "completed",
            "the outer guard must preserve already-completed work even when continuations are scheduled asynchronously.");

        var pending = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        try
        {
            await WithOuterGuardAsync(
                () => pending.Task,
                "outer guard must reject stalled work.",
                TimeSpan.FromMilliseconds(10));
        }
        catch (TimeoutException ex)
        {
            Assert(ex.Message == "outer guard must reject stalled work.",
                "the outer guard must retain its caller-provided timeout diagnostic.");
            return;
        }

        throw new InvalidOperationException("the outer guard accepted stalled work.");
    }

    private static async Task AssertP3ActionFailureMatrixRollsBackEntireTransactionAsync()
    {
        var cases = new[]
        {
            new P3ActionFailureCase(
                "script-throw",
                """
                async (identity, params, host) => {
                  await host.setFieldValue('direct_effect', 'staged-script');
                  await host.createRelationLink('p3_tx_link', params.targetId, { source: 'script-throw' });
                  throw new Error('script operation failed');
                }
                """,
                "async () => {}"),
            new P3ActionFailureCase(
                "host-failure",
                """
                async (identity, params, host) => {
                  await host.setFieldValue('direct_effect', 'staged-host');
                  await host.createRelationLink('p3_tx_link', params.targetId, { source: 'host-failure' });
                  await host.setFieldValue('missing_attribute', 'must-fail');
                  return [];
                }
                """,
                "async () => {}"),
            new P3ActionFailureCase(
                "returned-mutation-failure",
                """
                async (identity, params, host) => {
                  await host.setFieldValue('direct_effect', 'staged-returned');
                  await host.createRelationLink('p3_tx_link', params.targetId, { source: 'returned-failure' });
                  return [
                    { mutation: 'good_mutation', params: { value: 'staged-mutation' } },
                    { mutation: 'failing_mutation' }
                  ];
                }
                """,
                "async () => {}"),
            new P3ActionFailureCase(
                "after-interceptor-failure",
                """
                async (identity, params, host) => {
                  await host.setFieldValue('direct_effect', 'staged-after');
                  await host.createRelationLink('p3_tx_link', params.targetId, { source: 'after-failure' });
                  return [{ mutation: 'good_mutation', params: { value: 'staged-mutation' } }];
                }
                """,
                """
                async (identity, host) => {
                  await host.setFieldValue('interceptor_effect', 'staged-after');
                  throw new Error('after interceptor failed');
                }
                """),
        };

        foreach (var failure in cases)
        {
            using var db = new CozoDb(engine: "mem", path: "");
            var objectId = "p3:tx:" + failure.Name;
            var om = await CreateP3TransactionOwnerAsync(new CozoDbOmStore(db), objectId);
            var catalog = new BehaviorCatalog([
                Operation("P3TxChild", "failure_operation", "p3:tx:failure:operation"),
                Mutation("P3TxChild", "good_mutation", "p3:tx:failure:good-mutation"),
                Mutation("P3TxChild", "failing_mutation", "p3:tx:failure:failing-mutation"),
                Interceptor("P3TxChild", "failure_operation", "before", 0, "p3:tx:failure:before"),
                Interceptor("P3TxChild", "failure_operation", "after", 0, "p3:tx:failure:after"),
            ]);
            var definitions = ImmutableArray.Create(
                new JintBehaviorScriptDefinition(
                    "p3:tx:failure:operation",
                    failure.OperationSource,
                    failure.Name + "-operation.js"),
                new JintBehaviorScriptDefinition(
                    "p3:tx:failure:good-mutation",
                    """
                    async (identity, params, host) => {
                      await host.setFieldValue('mutation_effect', params.value);
                    }
                    """,
                    failure.Name + "-mutation.js"),
                new JintBehaviorScriptDefinition(
                    "p3:tx:failure:before",
                    """
                    async (identity, host) => {
                      await host.setFieldValue('interceptor_effect', 'staged-before');
                    }
                    """,
                    failure.Name + "-before.js"),
                new JintBehaviorScriptDefinition(
                    "p3:tx:failure:after",
                    failure.AfterSource,
                    failure.Name + "-after.js"));
            var callbacks = new BehaviorCallbackBindingSet(mutations:
            [
                new("p3:tx:failure:failing-mutation", (_, _) =>
                    throw new InvalidOperationException("returned mutation failed")),
            ]);
            await ImportReadyP3CatalogAsync(om, catalog, definitions, callbacks);

            await WithOuterGuardAsync(
                () => ExpectFailureAsync(
                    () => om.ExecuteOperationAsync(
                        objectId,
                        "failure_operation",
                        new Dictionary<string, object?> { ["targetId"] = "p3:tx:target" }),
                    failure.Name + " must surface an operation failure."),
                failure.Name + " did not terminate.");
            await AssertP3TransactionStateAsync(
                om,
                objectId,
                expectedLinkCount: 0,
                failure.Name + " must roll back property, relation, mutation and interceptor effects.");
        }
    }

    private static async Task AssertP3CancellationRollsBackWithoutLateEffectsAsync()
    {
        using var db = new CozoDb(engine: "mem", path: "");
        var blockingStore = new P3BlockingOmStore(new CozoDbOmStore(db), "p3_cancel_probe");
        var om = await CreateP3TransactionOwnerAsync(blockingStore, "p3:tx:cancellation");
        using var cancellation = new CancellationTokenSource();
        var catalog = new BehaviorCatalog([
            Operation("P3TxChild", "cancel_operation", "p3:tx:cancel:operation"),
            Mutation("P3TxChild", "good_mutation", "p3:tx:cancel:mutation"),
            Interceptor("P3TxChild", "cancel_operation", "before", 0, "p3:tx:cancel:before"),
        ]);
        var definitions = ImmutableArray.Create(
            new JintBehaviorScriptDefinition(
                "p3:tx:cancel:operation",
                """
                async (identity, params, host) => {
                  await host.setFieldValue('direct_effect', 'cancel-staged');
                  await host.createRelationLink('p3_tx_link', params.targetId, { source: 'cancel' });
                  await host.getFieldValue('p3_cancel_probe');
                  await host.setFieldValue('direct_effect', 'late-effect');
                  return [{ mutation: 'good_mutation', params: { value: 'late-mutation' } }];
                }
                """,
                "p3-tx-cancel-operation.js"),
            new JintBehaviorScriptDefinition(
                "p3:tx:cancel:mutation",
                "async (identity, params, host) => { await host.setFieldValue('mutation_effect', params.value); }",
                "p3-tx-cancel-mutation.js"),
            new JintBehaviorScriptDefinition(
                "p3:tx:cancel:before",
                "async (identity, host) => { await host.setFieldValue('interceptor_effect', 'cancel-before'); }",
                "p3-tx-cancel-before.js"));
        await ImportReadyP3CatalogAsync(
            om,
            catalog,
            definitions,
            options: new JintBehaviorScriptOptions(
                Timeout: TimeSpan.FromSeconds(2),
                CancellationToken: cancellation.Token));

        blockingStore.Arm();
        var execution = om.ExecuteOperationAsync(
            "p3:tx:cancellation",
            "cancel_operation",
            new Dictionary<string, object?> { ["targetId"] = "p3:tx:target" });
        await WithOuterGuardAsync(
            () => blockingStore.Started,
            "cancellation probe did not reach the blocked async host read.");
        cancellation.Cancel();
        await WithOuterGuardAsync(
            () => ExpectOperationCanceledAsync(
                () => execution,
                "cancellation during async host work must surface OperationCanceledException."),
            "cancelled operation did not unwind.");
        await WithOuterGuardAsync(
            () => blockingStore.Unwound,
            "blocked host operation did not observe cancellation.");

        await AssertP3TransactionStateAsync(
            om,
            "p3:tx:cancellation",
            expectedLinkCount: 0,
            "cancellation must roll back all effects staged before the blocked host operation.");
        await Task.Delay(100);
        await AssertP3TransactionStateAsync(
            om,
            "p3:tx:cancellation",
            expectedLinkCount: 0,
            "cancelled host work must not publish delayed property, relation, mutation or interceptor effects.");
    }

    private static async Task<CozoOm> CreateP3TransactionOwnerAsync(
        ICozoOmStore store,
        string objectId)
    {
        var om = new CozoOm(store);
        await om.InitSchemaAsync();
        await om.DefineClassAsync("P3TxBase", "P3 script transaction base");
        await om.DefineClassAsync("P3TxChild", "P3 script transaction child", parentClass: "P3TxBase");
        await om.DefineFieldAsync("P3TxBase", "trace", OmValueType.String);
        await om.DefineFieldAsync("P3TxBase", "direct_effect", OmValueType.String);
        await om.DefineFieldAsync("P3TxBase", "mutation_effect", OmValueType.String);
        await om.DefineFieldAsync("P3TxBase", "child_effect", OmValueType.String);
        await om.DefineFieldAsync("P3TxBase", "interceptor_effect", OmValueType.String);
        await om.DefineFieldAsync("P3TxBase", "p3_cancel_probe", OmValueType.String);
        await om.DefineRelationDefAsync("p3_tx_link", "P3TxBase", "P3TxBase");
        await om.CreateObjectAsync(objectId, "P3TxChild", "P3 transaction subject");
        await om.CreateObjectAsync("p3:tx:target", "P3TxBase", "P3 transaction target");
        await om.SetFieldValueAsync(objectId, "trace", "initial");
        await om.SetFieldValueAsync(objectId, "direct_effect", "initial-direct");
        await om.SetFieldValueAsync(objectId, "mutation_effect", "initial-mutation");
        await om.SetFieldValueAsync(objectId, "child_effect", "initial-child");
        await om.SetFieldValueAsync(objectId, "interceptor_effect", "initial-interceptor");
        await om.SetFieldValueAsync(objectId, "p3_cancel_probe", "initial-probe");
        return om;
    }

    private static async Task ImportReadyP3CatalogAsync(
        CozoOm om,
        BehaviorCatalog catalog,
        IEnumerable<JintBehaviorScriptDefinition> definitions,
        BehaviorCallbackBindingSet? nativeCallbacks = null,
        JintBehaviorScriptOptions? options = null)
    {
        var canonicalJson = BehaviorManifestJsonCodec.Encode(catalog);
        var decoded = BehaviorManifestJsonCodec.Decode(canonicalJson);
        Assert(decoded.Success && decoded.Catalog is not null,
            "T3.2 canonical behavior catalog must decode before binding.");
        var provider = JintBehaviorScriptProvider.BuildBindings(new JintBehaviorScriptBindingRequest(
            decoded.Catalog!,
            definitions,
            options,
            nativeCallbacks));
        Assert(provider.Success,
            "T3.2 script/native provider must bind every exact callback before canonical import: "
            + string.Join(",", provider.Diagnostics.Select(item => item.Code + "/" + item.BindingId)));
        var import = await om.ImportBehaviorManifestJsonAsync(
            Encoding.UTF8.GetString(canonicalJson),
            provider.Bindings,
            new BehaviorImportOptions(RequireReady: true));
        Assert(import is { Applied: true, Diagnostics.Length: 0, Unresolved.Length: 0 },
            "T3.2 canonical RequireReady import must atomically publish the complete operation pipeline.");
    }

    private static async ValueTask AppendP3TraceAsync(
        OmMutationContext context,
        string entry)
    {
        var current = AsJsonString(await context.GetFieldValueAsync("trace")) ?? "";
        await context.SetFieldValueAsync("trace", current + "," + entry);
    }

    private static async Task AssertP3TransactionStateAsync(
        CozoOm om,
        string objectId,
        int expectedLinkCount,
        string message)
    {
        Assert(AsJsonString(await om.GetFieldValueAsync(objectId, "direct_effect")) == "initial-direct"
               && AsJsonString(await om.GetFieldValueAsync(objectId, "mutation_effect")) == "initial-mutation"
               && AsJsonString(await om.GetFieldValueAsync(objectId, "child_effect")) == "initial-child"
               && AsJsonString(await om.GetFieldValueAsync(objectId, "interceptor_effect")) == "initial-interceptor"
               && AsJsonString(await om.GetFieldValueAsync(objectId, "trace")) == "initial"
               && (await om.GetNeighborsAsync(objectId, "p3_tx_link", OmDirection.Outgoing)).Outgoing.Count
                   == expectedLinkCount,
            message);
    }

    private static async Task ExpectFailureAsync(Func<Task> operation, string message)
    {
        try
        {
            await operation();
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return;
        }

        throw new InvalidOperationException(message);
    }

    private static BehaviorCatalog P3IntegrationCatalog() =>
        new([
            new BehaviorCatalogEntry(
                BehaviorCatalogKind.Constraint,
                "P3ScriptOwner",
                "script_conditional",
                "conditional",
                "P3 conditional failed",
                null,
                null,
                null,
                [
                    new(BehaviorCatalogCallbackSlot.When, "p3:script:constraint:when", BehaviorReadiness.Unresolved),
                    new(BehaviorCatalogCallbackSlot.Then, "p3:script:constraint:then", BehaviorReadiness.Unresolved),
                ]),
            Constraint(
                "P3ScriptOwner",
                "script_validator",
                BehaviorCatalogCallbackSlot.Validator,
                "p3:script:constraint:validator"),
            ComputedProp("P3ScriptOwner", "script_computed_prop", "p3:script:computedProp"),
            ComputedProp("P3ScriptOwner", "native_computed_prop", "p3:native:computedProp"),
            Operation("P3ScriptOwner", "script_operation", "p3:script:operation"),
            Mutation("P3ScriptOwner", "script_mutation", "p3:script:mutation"),
            Interceptor(
                "P3ScriptOwner",
                "script_operation",
                "after",
                7,
                "p3:script:interceptor"),
        ]);

    private static ImmutableArray<JintBehaviorScriptDefinition> P3IntegrationDefinitions() =>
        [
            new(
                "p3:script:constraint:when",
                "async (identity, host) => (await host.getFieldValue('validation_gate')) === 'ready'",
                "p3-constraint-when.js"),
            new(
                "p3:script:constraint:then",
                "async (identity, host) => (await host.getFieldValue('validation_gate')) === 'ready'",
                "p3-constraint-then.js"),
            new(
                "p3:script:constraint:validator",
                "async (identity, host) => (await host.getFieldValue('validation_gate')) === 'ready' ? null : 'P3 validator failed'",
                "p3-constraint-validator.js"),
            new(
                "p3:script:computedProp",
                "(identity) => ({ engine: 'jint', objectId: identity.objectId })",
                "p3-computedProp.js"),
            new(
                "p3:script:operation",
                "(identity, params) => [{ mutation: 'script_mutation', params: { value: params.value } }]",
                "p3-operation.js"),
            new(
                "p3:script:mutation",
                "async (identity, params, host) => { await host.setFieldValue('effect', params.value); }",
                "p3-mutation.js"),
            new(
                "p3:script:interceptor",
                "async (identity, host) => { await host.setFieldValue('interceptor_marker', 'after-script-operation'); }",
                "p3-interceptor.js"),
        ];

    private static BehaviorCallbackBindingSet P3NativeCallbacks() =>
        new(computedProp:
        [
            new("p3:native:computedProp", _ => ValueTask.FromResult<object?>("native-ready")),
        ]);

    private static async Task<CozoOm> CreateP3OwnerAsync(CozoDb db, string objectId)
    {
        var om = new CozoOm(db);
        await om.InitSchemaAsync();
        await om.DefineClassAsync("P3ScriptOwner", "P3 script integration owner");
        await om.DefineFieldAsync("P3ScriptOwner", "validation_gate", OmValueType.String);
        await om.DefineFieldAsync("P3ScriptOwner", "effect", OmValueType.String);
        await om.DefineFieldAsync("P3ScriptOwner", "interceptor_marker", OmValueType.String);
        await om.CreateObjectAsync(objectId, "P3ScriptOwner", "P3 integration entity");
        await om.SetFieldValueAsync(objectId, "validation_gate", "ready");
        await om.SetFieldValueAsync(objectId, "effect", "initial");
        await om.SetFieldValueAsync(objectId, "interceptor_marker", "initial");
        return om;
    }

    private static async Task<CozoOm> CreateReadyComputedScriptAsync(
        CozoDb db,
        string objectId,
        string bindingId,
        string source,
        JintBehaviorScriptOptions options)
    {
        var om = new CozoOm(db);
        await om.InitSchemaAsync();
        await om.DefineClassAsync("P3ProbeOwner", "P3 integrated script probe owner");
        await om.CreateObjectAsync(objectId, "P3ProbeOwner", "P3 probe entity");
        var catalog = new BehaviorCatalog([
            ComputedProp("P3ProbeOwner", "probe", bindingId),
        ]);
        var canonicalJson = BehaviorManifestJsonCodec.Encode(catalog);
        var decoded = BehaviorManifestJsonCodec.Decode(canonicalJson);
        var provider = JintBehaviorScriptProvider.BuildBindings(new JintBehaviorScriptBindingRequest(
            decoded.Catalog!,
            [new(bindingId, source, bindingId.Split(':').Last() + ".js")],
            options));
        Assert(provider.Success, $"integrated probe provider must bind {bindingId} before import.");
        var import = await om.ImportBehaviorManifestJsonAsync(
            Encoding.UTF8.GetString(canonicalJson),
            provider.Bindings,
            new BehaviorImportOptions(RequireReady: true));
        Assert(import is { Applied: true, Diagnostics.Length: 0, Unresolved.Length: 0 }
               && (await om.GetBehaviorCatalogAsync()).Behaviors.Single().Callbacks.Single().Readiness
                   == BehaviorReadiness.Ready,
            $"integrated probe {bindingId} must be Ready before execution.");
        return om;
    }

    private static void AssertCanonicalManifestHasNoScriptRuntimeSchema(
        ReadOnlySpan<byte> canonicalJson,
        IEnumerable<JintBehaviorScriptDefinition> definitions)
    {
        using var document = JsonDocument.Parse(canonicalJson.ToArray());
        var propertyNames = EnumerateJsonPropertyNames(document.RootElement).ToImmutableArray();
        Assert(propertyNames.All(name => !name.Contains("source", StringComparison.OrdinalIgnoreCase)
                                         && !name.Contains("jint", StringComparison.OrdinalIgnoreCase)
                                         && !name.Contains("scriptRuntime", StringComparison.OrdinalIgnoreCase)),
            "canonical manifest schema must contain no script source or Jint-specific property.");

        var json = Encoding.UTF8.GetString(canonicalJson);
        Assert(definitions.All(definition => !json.Contains(definition.Source, StringComparison.Ordinal)
                                             && (definition.SourceName is null
                                                 || !json.Contains(definition.SourceName, StringComparison.Ordinal))),
            "canonical manifest payload must persist behavior metadata and binding ids, never script source/sourceName.");
        Assert(json.Contains("bindingId", StringComparison.Ordinal)
               && json.Contains("ownerClass", StringComparison.Ordinal)
               && json.Contains("readiness", StringComparison.Ordinal),
            "canonical manifest must retain provider-neutral behavior metadata and exact binding ids.");
    }

    private static IEnumerable<string> EnumerateJsonPropertyNames(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in element.EnumerateObject())
            {
                yield return property.Name;
                foreach (var nested in EnumerateJsonPropertyNames(property.Value))
                {
                    yield return nested;
                }
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray())
            {
                foreach (var nested in EnumerateJsonPropertyNames(item))
                {
                    yield return nested;
                }
            }
        }
    }

    private static void AssertProviderStateUnchanged(
        ProviderValidationState before,
        ProviderValidationState after,
        string message)
    {
        Assert(before.Catalog.SequenceEqual(after.Catalog)
               && before.BindingRows.SequenceEqual(after.BindingRows)
               && before.Readiness.SequenceEqual(after.Readiness)
               && ReferenceEquals(before.RegistrySnapshot, after.RegistrySnapshot),
            message);
    }

    private static void AssertAllBindingsEmpty(BehaviorCallbackBindingSet bindings, string message)
    {
        Assert(bindings.Constraints.IsEmpty
               && bindings.Validators.IsEmpty
               && bindings.ComputedProps.IsEmpty
               && bindings.Operations.IsEmpty
               && bindings.Mutations.IsEmpty
               && bindings.Interceptors.IsEmpty,
            message);
    }

    private static async Task ExpectBehaviorUnresolvedAsync(Func<Task> operation, string message)
    {
        try
        {
            await operation();
        }
        catch (BehaviorUnresolvedException)
        {
            return;
        }

        throw new InvalidOperationException(message);
    }

    private static BehaviorCatalog AllCallbackShapesCatalog() =>
        new([
            new BehaviorCatalogEntry(
                BehaviorCatalogKind.Constraint,
                "ScriptOwner",
                "script_conditional",
                "conditional",
                "script conditional message",
                null,
                null,
                null,
                [
                    new(BehaviorCatalogCallbackSlot.When, "script:constraint:when", BehaviorReadiness.Unresolved),
                    new(BehaviorCatalogCallbackSlot.Then, "script:constraint:then", BehaviorReadiness.Unresolved),
                ]),
            Constraint("ScriptOwner", "script_validator", BehaviorCatalogCallbackSlot.Validator, "script:constraint:validator"),
            ComputedProp("ScriptOwner", "script_computed_prop", "script:computedProp"),
            Operation("ScriptOwner", "script_operation", "script:operation"),
            Mutation("ScriptOwner", "script_mutation", "script:mutation"),
            Interceptor("ScriptOwner", "script_operation", "after", 7, "script:interceptor"),
        ]);

    private static ImmutableArray<JintBehaviorScriptDefinition> AllCallbackShapeDefinitions() =>
        [
            new("script:constraint:when", "() => true"),
            new("script:constraint:then", "() => true"),
            new("script:constraint:validator", "() => null"),
            new("script:computedProp", "() => 42"),
            new("script:operation", "() => []"),
            new("script:mutation", "() => null"),
            new("script:interceptor", "() => null"),
        ];

    private static ImmutableArray<JintBehaviorScriptDefinition> ExecutableCallbackShapeDefinitions() =>
        [
            new("script:constraint:when", "ctx => ctx.objectId === 'script:1'"),
            new("script:constraint:then", "() => true"),
            new("script:constraint:validator", "() => null"),
            new("script:computedProp", "() => { globalThis.count = (globalThis.count || 0) + 1; return { score: 42, count: globalThis.count, tags: ['jint'] }; }"),
            new("script:operation", "() => [{ mutation: 'script_mutation', params: { answer: 42 } }]"),
            new("script:mutation", "() => null"),
            new("script:interceptor", "() => null"),
        ];

    private static BehaviorCatalog ReverseCatalog(BehaviorCatalog catalog) =>
        new(catalog.Behaviors
            .Reverse()
            .Select(entry => entry with { Callbacks = entry.Callbacks.Reverse().ToImmutableArray() }));

    private static ImmutableArray<string> BindingIdSequences(JintBehaviorScriptBindingResult result) =>
        result.Bindings.Constraints.Select(binding => "constraint:" + binding.BindingId)
            .Concat(result.Bindings.Validators.Select(binding => "validator:" + binding.BindingId))
            .Concat(result.Bindings.ComputedProps.Select(binding => "computedProp:" + binding.BindingId))
            .Concat(result.Bindings.Operations.Select(binding => "operation:" + binding.BindingId))
            .Concat(result.Bindings.Mutations.Select(binding => "mutation:" + binding.BindingId))
            .Concat(result.Bindings.Interceptors.Select(binding => "interceptor:" + binding.BindingId))
            .ToImmutableArray();

    private static BehaviorCatalogEntry Constraint(
        string owner,
        string name,
        BehaviorCatalogCallbackSlot slot,
        string bindingId) =>
        new(
            BehaviorCatalogKind.Constraint,
            owner,
            name,
            slot == BehaviorCatalogCallbackSlot.Validator ? "custom" : "conditional",
            $"{name} message",
            null,
            null,
            null,
            [new(slot, bindingId, BehaviorReadiness.Unresolved)]);

    private static BehaviorCatalogEntry ComputedProp(string owner, string name, string bindingId) =>
        new(
            BehaviorCatalogKind.ComputedProp,
            owner,
            name,
            null,
            null,
            $"{name} description",
            null,
            null,
            [new(BehaviorCatalogCallbackSlot.Compute, bindingId, BehaviorReadiness.Unresolved)]);

    private static BehaviorCatalogEntry Operation(string owner, string name, string bindingId) =>
        new(
            BehaviorCatalogKind.Operation,
            owner,
            name,
            null,
            null,
            $"{name} description",
            null,
            null,
            [new(BehaviorCatalogCallbackSlot.Handler, bindingId, BehaviorReadiness.Unresolved)]);

    private static BehaviorCatalogEntry Mutation(string owner, string name, string bindingId) =>
        new(
            BehaviorCatalogKind.Mutation,
            owner,
            name,
            null,
            null,
            $"{name} description",
            null,
            null,
            [new(BehaviorCatalogCallbackSlot.Executor, bindingId, BehaviorReadiness.Unresolved)]);

    private static BehaviorCatalogEntry Interceptor(
        string owner,
        string name,
        string phase,
        int seq,
        string bindingId) =>
        new(
            BehaviorCatalogKind.Interceptor,
            owner,
            name,
            null,
            null,
            $"{phase} {name} interceptor",
            phase,
            seq,
            [new(BehaviorCatalogCallbackSlot.Handler, bindingId, BehaviorReadiness.Unresolved)]);

    private static async Task<ProviderValidationState> CaptureProviderValidationStateAsync(CozoOm om)
    {
        var catalog = await om.GetBehaviorCatalogAsync();
        return new ProviderValidationState(
            BehaviorManifestJsonCodec.Encode(catalog).ToImmutableArray(),
            (await BehaviorBindingLogic.ListAsync(om.Runtime)).ToImmutableArray(),
            catalog.Behaviors
                .SelectMany(entry => entry.Callbacks.Select(callback => new ProviderReadinessProbe(
                    entry.Kind,
                    entry.OwnerClass,
                    entry.Name,
                    entry.InterceptorPhase,
                    entry.InterceptorSeq,
                    callback.Slot,
                    callback.BindingId,
                    callback.Readiness)))
                .OrderBy(entry => entry.Kind)
                .ThenBy(entry => entry.OwnerClass, StringComparer.Ordinal)
                .ThenBy(entry => entry.Name, StringComparer.Ordinal)
                .ThenBy(entry => entry.InterceptorPhase ?? "", StringComparer.Ordinal)
                .ThenBy(entry => entry.InterceptorSeq ?? -1)
                .ThenBy(entry => entry.Slot)
                .ThenBy(entry => entry.BindingId ?? "", StringComparer.Ordinal)
                .ToImmutableArray(),
            om.Runtime.Registry.CaptureSnapshot());
    }

    private static async Task<P2CallbackContexts> CreateP2CallbackContextsAsync(CozoDb db)
    {
        var om = new CozoOm(db);
        await om.InitSchemaAsync();
        await om.DefineClassAsync("ScriptOwner", "P2 script owner");
        await om.CreateObjectAsync("p2:1", "ScriptOwner", "P2 entity");
        return new P2CallbackContexts(
            new OmValidationContext(om.Runtime, "p2:1", "ScriptOwner"),
            new OmComputedPropContext(om.Runtime, "p2:1", "ScriptOwner"),
            new OmOperationContext(
                om.Runtime,
                "p2:1",
                "ScriptOwner",
                "ScriptOwner",
                new Dictionary<string, object?> { ["origin"] = "p2-test" }),
            new OmMutationContext(om.Runtime, "p2:1", "ScriptOwner"));
    }

    private static async Task<JintBehaviorScriptException> ExpectScriptExceptionAsync(
        Func<Task> operation,
        string code,
        string bindingId,
        BehaviorCatalogKind kind,
        BehaviorCatalogCallbackSlot slot,
        JintBehaviorScriptFailurePhase phase,
        string? sourceName,
        string forbiddenFullSource)
    {
        try
        {
            await operation();
        }
        catch (JintBehaviorScriptException ex)
        {
            Assert(ex.Code == code
                   && ex.BindingId == bindingId
                   && ex.Kind == kind
                   && ex.Slot == slot
                   && ex.Phase == phase
                   && ex.SourceName == sourceName
                   && ex.InnerException is not null,
                $"script failure must expose stable code/binding/kind/slot/phase/source identity for {bindingId}: {ex}");
            Assert(ex.Line is null or > 0, "script failure line, when provided, must be positive.");
            Assert(ex.Column is null or > 0, "script failure column, when provided, must be positive.");
            Assert(!ex.Message.Contains(forbiddenFullSource, StringComparison.Ordinal)
                   && !ex.ToString().Contains(forbiddenFullSource, StringComparison.Ordinal),
                "script failures must not embed the full source text in public exception messages or ToString output.");
            return ex;
        }

        throw new InvalidOperationException($"expected JintBehaviorScriptException code {code} for {bindingId}");
    }

    private static async Task ExpectOperationCanceledAsync(Func<Task> operation, string message)
    {
        try
        {
            await operation();
        }
        catch (JintBehaviorScriptException ex)
        {
            throw new InvalidOperationException($"{message} It was mapped to ordinary script failure {ex.Code}/{ex.Phase}.", ex);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        throw new InvalidOperationException(message);
    }

    private static async Task<T> WithOuterGuardAsync<T>(
        Func<Task<T>> operation,
        string message,
        TimeSpan? timeout = null)
    {
        var task = operation();
        try
        {
            return await task.WaitAsync(timeout ?? OuterGuardTimeout);
        }
        catch (TimeoutException ex)
        {
            throw new TimeoutException(message, ex);
        }
    }

    private static async Task WithOuterGuardAsync(
        Func<Task> operation,
        string message,
        TimeSpan? timeout = null)
    {
        await WithOuterGuardAsync(async () =>
        {
            await operation();
            return true;
        }, message, timeout);
    }

    private static void AssertRejectedBeforeBindings(
        JintBehaviorScriptBindingResult result,
        string code,
        string bindingId,
        string message)
    {
        Assert(!result.Success
               && result.Bindings.Constraints.IsEmpty
               && result.Bindings.Validators.IsEmpty
               && result.Bindings.ComputedProps.IsEmpty
               && result.Bindings.Operations.IsEmpty
               && result.Bindings.Mutations.IsEmpty
               && result.Bindings.Interceptors.IsEmpty
               && result.Diagnostics.Any(diagnostic => diagnostic.Code == code && diagnostic.BindingId == bindingId),
            message);
    }

    private static bool HasProjectReference(XDocument project, string include) =>
        project.Descendants()
            .Any(element => element.Name.LocalName == "ProjectReference"
                            && string.Equals((string?)element.Attribute("Include"), include, StringComparison.Ordinal));

    private static string FindRepoRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "src", "Depa.Ontology", "Depa.Ontology.csproj")))
            {
                return current.FullName;
            }

            var siblingOntologyRoot = Path.Combine(current.FullName, "depa-ontology.cs");
            if (File.Exists(Path.Combine(siblingOntologyRoot, "src", "Depa.Ontology", "Depa.Ontology.csproj")))
            {
                return siblingOntologyRoot;
            }

            current = current.Parent;
        }

        throw new InvalidOperationException("Could not locate repository root from test output directory.");
    }

    private static bool RejectsImmutableArrayMutation<T>(ImmutableArray<T> values, T replacement)
    {
        try
        {
            ((IList<T>)values)[0] = replacement;
            return false;
        }
        catch (NotSupportedException)
        {
            return true;
        }
    }

    private static string? AsJsonString(JsonElement? value) =>
        value is { ValueKind: JsonValueKind.String } element ? element.GetString() : null;

    private static string? AsJsonStringValue(object? value) => value switch
    {
        string text => text,
        JsonElement { ValueKind: JsonValueKind.String } element => element.GetString(),
        _ => null,
    };

    private static void Assert(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    private sealed record ProviderValidationState(
        ImmutableArray<byte> Catalog,
        ImmutableArray<BehaviorBindingRow> BindingRows,
        ImmutableArray<ProviderReadinessProbe> Readiness,
        CozoOmRegistrySnapshot RegistrySnapshot);

    private sealed record P2CallbackContexts(
        OmValidationContext Validation,
        OmComputedPropContext ComputedProp,
        OmOperationContext Operation,
        OmMutationContext Mutation);

    private sealed record InvalidResultCase(
        string Name,
        BehaviorCatalogEntry Entry,
        string BindingId,
        string Source,
        BehaviorCatalogKind Kind,
        BehaviorCatalogCallbackSlot Slot,
        Func<JintBehaviorScriptBindingResult, P2CallbackContexts, Task> Invoke);

    private sealed record LimitCase(
        string Name,
        string BindingId,
        string Source,
        JintBehaviorScriptOptions Options,
        string Code,
        JintBehaviorScriptFailurePhase Phase);

    private sealed record P3IntegratedFailureCase(
        string Name,
        string BindingId,
        string Source,
        JintBehaviorScriptOptions Options,
        string Code,
        JintBehaviorScriptFailurePhase Phase);

    private sealed record P3ActionFailureCase(
        string Name,
        string OperationSource,
        string AfterSource);

    private sealed record ProviderReadinessProbe(
        BehaviorCatalogKind Kind,
        string OwnerClass,
        string Name,
        string? InterceptorPhase,
        int? InterceptorSeq,
        BehaviorCatalogCallbackSlot Slot,
        string? BindingId,
        BehaviorReadiness Readiness);

    private sealed class MutationSpecComparer : IEqualityComparer<MutationSpec>
    {
        public static readonly MutationSpecComparer Instance = new();

        public bool Equals(MutationSpec? x, MutationSpec? y) =>
            x is not null
            && y is not null
            && string.Equals(x.Mutation, y.Mutation, StringComparison.Ordinal)
            && DictionaryEquals(x.Params, y.Params);

        public int GetHashCode(MutationSpec obj) => obj.Mutation.GetHashCode(StringComparison.Ordinal);

        private static bool DictionaryEquals(
            IReadOnlyDictionary<string, object?>? left,
            IReadOnlyDictionary<string, object?>? right)
        {
            if (left is null || right is null)
            {
                return left is null && right is null;
            }

            return left.Count == right.Count
                   && left.OrderBy(pair => pair.Key, StringComparer.Ordinal).SequenceEqual(
                       right.OrderBy(pair => pair.Key, StringComparer.Ordinal),
                       KeyValuePairComparer.Instance);
        }
    }

    private sealed class KeyValuePairComparer : IEqualityComparer<KeyValuePair<string, object?>>
    {
        public static readonly KeyValuePairComparer Instance = new();

        public bool Equals(KeyValuePair<string, object?> x, KeyValuePair<string, object?> y) =>
            string.Equals(x.Key, y.Key, StringComparison.Ordinal)
            && Equals(NormalizeNumber(x.Value), NormalizeNumber(y.Value));

        public int GetHashCode(KeyValuePair<string, object?> obj) => obj.Key.GetHashCode(StringComparison.Ordinal);

        private static object? NormalizeNumber(object? value) =>
            value is IConvertible and not string and not bool ? Convert.ToDouble(value) : value;
    }

    private sealed class P3BlockingOmStore : ICozoOmStore
    {
        private readonly ICozoOmStore _inner;
        private readonly string _parameterMarker;
        private readonly TaskCompletionSource _started =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource _unwound =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _armed;

        public P3BlockingOmStore(ICozoOmStore inner, string parameterMarker)
        {
            _inner = inner;
            _parameterMarker = parameterMarker;
        }

        public Task Started => _started.Task;

        public Task Unwound => _unwound.Task;

        public void Arm() => Volatile.Write(ref _armed, 1);

        public Task<OmQueryResult> RunAsync(
            string script,
            object? parameters = null,
            bool immutable = false,
            CancellationToken cancellationToken = default) =>
            RunCoreAsync(_inner, script, parameters, immutable, cancellationToken);

        public async Task<ICozoOmTransaction> BeginTransactionAsync(
            bool write = true,
            CancellationToken cancellationToken = default) =>
            new P3BlockingOmTransaction(
                this,
                await _inner.BeginTransactionAsync(write, cancellationToken));

        private async Task<OmQueryResult> RunCoreAsync(
            ICozoOmStore inner,
            string script,
            object? parameters,
            bool immutable,
            CancellationToken cancellationToken)
        {
            if (Volatile.Read(ref _armed) == 1
                && JsonSerializer.Serialize(parameters).Contains(_parameterMarker, StringComparison.Ordinal)
                && Interlocked.Exchange(ref _armed, 0) == 1)
            {
                _started.TrySetResult();
                try
                {
                    await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
                }
                finally
                {
                    _unwound.TrySetResult();
                }
            }

            return await inner.RunAsync(script, parameters, immutable, cancellationToken);
        }

        private sealed class P3BlockingOmTransaction(
            P3BlockingOmStore owner,
            ICozoOmTransaction inner) : ICozoOmTransaction
        {
            public Task<OmQueryResult> RunAsync(
                string script,
                object? parameters = null,
                bool immutable = false,
                CancellationToken cancellationToken = default) =>
                owner.RunCoreAsync(inner, script, parameters, immutable, cancellationToken);

            public async Task<ICozoOmTransaction> BeginTransactionAsync(
                bool write = true,
                CancellationToken cancellationToken = default) =>
                new P3BlockingOmTransaction(
                    owner,
                    await inner.BeginTransactionAsync(write, cancellationToken));

            public Task CommitAsync(CancellationToken cancellationToken = default) =>
                inner.CommitAsync(cancellationToken);

            public Task AbortAsync(CancellationToken cancellationToken = default) =>
                inner.AbortAsync(cancellationToken);

            public ValueTask DisposeAsync() => inner.DisposeAsync();
        }
    }
}
