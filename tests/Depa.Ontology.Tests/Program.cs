using System.Collections.Immutable;
using System.Text;
using System.Text.Json;
using Depa.Cozo;
using Depa.Datalog;
using Depa.Datalog.Cozo;
using Depa.Ontology.Analytics;
using Depa.Ontology;
using Depa.Ontology.Query;
using Depa.Ontology.Batch;
using Depa.Ontology.Contracts.Models;
using Depa.Ontology.Contracts;
using Depa.Ontology.Inputs;
using Depa.Ontology.Logic;
using Depa.Ontology.Portability.Yaml;
using Depa.Ontology.Runtime;
using Depa.Ontology.Support;

static void Assert(bool condition, string message)
{
    if (!condition)
    {
        throw new InvalidOperationException(message);
    }
}

static bool RejectsImmutableArrayMutation<T>(ImmutableArray<T> values, T replacement)
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

static string? AsString(JsonElement? value)
{
    return value?.ValueKind == JsonValueKind.String ? value.Value.GetString() : null;
}

static double? AsNumber(JsonElement? value)
{
    return value?.ValueKind == JsonValueKind.Number && value.Value.TryGetDouble(out var number) ? number : null;
}

static bool? AsBool(JsonElement? value)
{
    return value?.ValueKind is JsonValueKind.True or JsonValueKind.False ? value.Value.GetBoolean() : null;
}

static JsonElement Rows(JsonDocument document)
{
    return document.RootElement.GetProperty("rows");
}

static async Task ExpectCozoExceptionAsync(Func<Task> operation, string messagePart, string assertMessage)
{
    try
    {
        await operation();
    }
    catch (CozoException ex) when (ex.Message.Contains(messagePart, StringComparison.OrdinalIgnoreCase))
    {
        return;
    }

    throw new InvalidOperationException(assertMessage);
}

static async Task ExpectExceptionAsync<TException>(Func<Task> operation, string assertMessage)
    where TException : Exception
{
    try
    {
        await operation();
    }
    catch (TException)
    {
        return;
    }

    throw new InvalidOperationException(assertMessage);
}

static async Task<BehaviorUnresolvedException> ExpectUnresolvedAsync(
    Func<Task> operation,
    BehaviorCatalogKind kind,
    string ownerClass,
    string behaviorKey,
    BehaviorCatalogCallbackSlot slot,
    string bindingId,
    string? interceptorPhase = null,
    int? interceptorSeq = null)
{
    try
    {
        await operation();
    }
    catch (BehaviorUnresolvedException ex)
    {
        var diagnostic = ex.Diagnostic;
        Assert(diagnostic.Code == "OMR1001"
               && diagnostic.Kind == kind
               && diagnostic.OwnerClass == ownerClass
               && diagnostic.BehaviorKey == behaviorKey
               && diagnostic.Slot == slot
               && diagnostic.BindingId == bindingId
               && diagnostic.InterceptorPhase == interceptorPhase
               && diagnostic.InterceptorSeq == interceptorSeq,
            $"unresolved diagnostic did not match the expected structured identity: {ex.Message}");
        return ex;
    }

    throw new InvalidOperationException($"expected unresolved behavior exception for {behaviorKey}");
}

static int DefinitionCount(CozoDb db, string kind, string owner, string name)
{
    var script = kind switch
    {
        "constraint" => "?[class_name, constraint_name] := *om_constraint_def{class_name, constraint_name}, class_name = $owner, constraint_name = $name",
        "computedProp" => "?[class_name, computed_prop_name] := *om_computed_prop_def{class_name, computed_prop_name}, class_name = $owner, computed_prop_name = $name",
        "operation" => "?[class_name, operation_name] := *om_operation_def{class_name, operation_name}, class_name = $owner, operation_name = $name",
        "mutation" => "?[class_name, mutation_name] := *om_mutation_def{class_name, mutation_name}, class_name = $owner, mutation_name = $name",
        "interceptor" => "?[class_name, operation_name] := *om_interceptor_def{class_name, operation_name}, class_name = $owner, operation_name = $name",
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown behavior definition kind"),
    };

    using var result = db.Run(script, new { owner, name });
    return Rows(result).GetArrayLength();
}

static string DefinitionPayload(
    CozoDb db,
    string kind,
    string owner,
    string name,
    string phase = "",
    int seq = 0)
{
    var script = kind switch
    {
        "constraint" => "?[constraint_kind, message] := *om_constraint_def{class_name: $owner, constraint_name: $name, constraint_kind, message}",
        "operation" => "?[description] := *om_operation_def{class_name: $owner, operation_name: $name, description}",
        "mutation" => "?[description] := *om_mutation_def{class_name: $owner, mutation_name: $name, description}",
        "interceptor" => "?[description] := *om_interceptor_def{class_name: $owner, operation_name: $name, phase: $phase, seq: $seq, description}",
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown behavior definition kind"),
    };

    using var result = db.Run(script, new { owner, name, phase, seq });
    var rows = Rows(result);
    Assert(rows.GetArrayLength() == 1, $"{kind} definition should have exactly one row for the requested key");
    return string.Join("|", rows[0].EnumerateArray().Select(value => value.GetString() ?? ""));
}

static ImmutableArray<BehaviorMetadataProbe> CaptureBehaviorMetadata(BehaviorCatalog catalog) =>
    catalog.Behaviors
        .Select(entry => new BehaviorMetadataProbe(
            entry.Kind,
            entry.OwnerClass,
            entry.Name,
            entry.ConstraintKind,
            entry.Message,
            entry.Description,
            entry.InterceptorPhase,
            entry.InterceptorSeq))
        .OrderBy(entry => entry.Kind)
        .ThenBy(entry => entry.OwnerClass, StringComparer.Ordinal)
        .ThenBy(entry => entry.Name, StringComparer.Ordinal)
        .ThenBy(entry => entry.InterceptorPhase ?? "", StringComparer.Ordinal)
        .ThenBy(entry => entry.InterceptorSeq ?? -1)
        .ToImmutableArray();

static ImmutableArray<BehaviorReadinessProbe> CaptureBehaviorReadiness(BehaviorCatalog catalog) =>
    catalog.Behaviors
        .SelectMany(entry => entry.Callbacks.Select(callback => new BehaviorReadinessProbe(
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
        .ToImmutableArray();

static ImmutableArray<RegistryBindingProbe> CaptureRegistryBindings(CozoOmRegistrySnapshot snapshot)
{
    var bindings = new List<RegistryBindingProbe>();
    bindings.AddRange(snapshot.Validators.Select(entry => new RegistryBindingProbe(
        BehaviorCatalogKind.Constraint,
        entry.Key.ClassName,
        entry.Key.ConstraintName,
        BehaviorCatalogCallbackSlot.Validator,
        null,
        null,
        entry.Value.BindingId,
        null)));
    bindings.AddRange(snapshot.Constraints.SelectMany(entry => new[]
    {
        new RegistryBindingProbe(
            BehaviorCatalogKind.Constraint,
            entry.Key.ClassName,
            entry.Key.ConstraintName,
            BehaviorCatalogCallbackSlot.When,
            null,
            null,
            entry.Value.WhenBindingId,
            null),
        new RegistryBindingProbe(
            BehaviorCatalogKind.Constraint,
            entry.Key.ClassName,
            entry.Key.ConstraintName,
            BehaviorCatalogCallbackSlot.Then,
            null,
            null,
            entry.Value.ThenBindingId,
            null),
    }));
    bindings.AddRange(snapshot.ComputedProps.Select(entry => new RegistryBindingProbe(
        BehaviorCatalogKind.ComputedProp,
        entry.Key.ClassName,
        entry.Key.ComputedPropName,
        BehaviorCatalogCallbackSlot.Compute,
        null,
        null,
        entry.Value.BindingId,
        null)));
    bindings.AddRange(snapshot.Operations.Select(entry => new RegistryBindingProbe(
        BehaviorCatalogKind.Operation,
        entry.Key.ClassName,
        entry.Key.OperationName,
        BehaviorCatalogCallbackSlot.Handler,
        null,
        null,
        entry.Value.BindingId,
        null)));
    bindings.AddRange(snapshot.Mutations.Select(entry => new RegistryBindingProbe(
        BehaviorCatalogKind.Mutation,
        entry.Key.ClassName,
        entry.Key.MutationName,
        BehaviorCatalogCallbackSlot.Executor,
        null,
        null,
        entry.Value.BindingId,
        null)));
    bindings.AddRange(snapshot.BeforeInterceptors.SelectMany(entry => entry.Value.Select(registration =>
        new RegistryBindingProbe(
            BehaviorCatalogKind.Interceptor,
            entry.Key.ClassName,
            entry.Key.OperationName,
            BehaviorCatalogCallbackSlot.Handler,
            "before",
            registration.Seq,
            registration.BindingId,
            registration.Description))));
    bindings.AddRange(snapshot.AfterInterceptors.SelectMany(entry => entry.Value.Select(registration =>
        new RegistryBindingProbe(
            BehaviorCatalogKind.Interceptor,
            entry.Key.ClassName,
            entry.Key.OperationName,
            BehaviorCatalogCallbackSlot.Handler,
            "after",
            registration.Seq,
            registration.BindingId,
            registration.Description))));

    return bindings
        .OrderBy(entry => entry.Kind)
        .ThenBy(entry => entry.OwnerClass, StringComparer.Ordinal)
        .ThenBy(entry => entry.Name, StringComparer.Ordinal)
        .ThenBy(entry => entry.Phase ?? "", StringComparer.Ordinal)
        .ThenBy(entry => entry.Seq ?? -1)
        .ThenBy(entry => entry.Slot)
        .ThenBy(entry => entry.BindingId ?? "", StringComparer.Ordinal)
        .ThenBy(entry => entry.Description ?? "", StringComparer.Ordinal)
        .ToImmutableArray();
}

static async Task<BehaviorStateProbe> CaptureBehaviorStateAsync(CozoOm om)
{
    var catalog = await om.GetBehaviorCatalogAsync();
    var registrySnapshot = om.Runtime.Registry.CaptureSnapshot();
    return new BehaviorStateProbe(
        BehaviorManifestJsonCodec.Encode(catalog).ToImmutableArray(),
        CaptureBehaviorMetadata(catalog),
        (await BehaviorBindingLogic.ListAsync(om.Runtime)).ToImmutableArray(),
        CaptureRegistryBindings(registrySnapshot),
        CaptureBehaviorReadiness(catalog),
        registrySnapshot);
}

static void AssertBehaviorStateUnchanged(
    BehaviorStateProbe before,
    BehaviorStateProbe after,
    string message)
{
    Assert(before.CanonicalCatalog.SequenceEqual(after.CanonicalCatalog)
           && before.Metadata.SequenceEqual(after.Metadata)
           && before.Bindings.SequenceEqual(after.Bindings)
           && before.RegistryBindings.SequenceEqual(after.RegistryBindings)
           && before.Readiness.SequenceEqual(after.Readiness)
           && ReferenceEquals(before.RegistrySnapshot, after.RegistrySnapshot),
        message);
}

static void AssertBehaviorStateEquivalent(
    BehaviorStateProbe expected,
    BehaviorStateProbe actual,
    string message)
{
    Assert(expected.CanonicalCatalog.SequenceEqual(actual.CanonicalCatalog)
           && expected.Metadata.SequenceEqual(actual.Metadata)
           && expected.Bindings.SequenceEqual(actual.Bindings)
           && expected.RegistryBindings.SequenceEqual(actual.RegistryBindings)
           && expected.Readiness.SequenceEqual(actual.Readiness),
        message);
}

static async Task<TException> CaptureExceptionAsync<TException>(Func<Task> operation, string assertMessage)
    where TException : Exception
{
    try
    {
        await operation();
    }
    catch (TException exception)
    {
        return exception;
    }

    throw new InvalidOperationException(assertMessage);
}

if (string.Equals(
        Environment.GetEnvironmentVariable("COZO_OM_TEST_FOCUS"),
        "permission-governance",
        StringComparison.Ordinal))
{
    HarnessDiagnostics.Start("permission governance parity fixture");
    await PermissionGovernanceParityFixtures.RunAsync();
    HarnessDiagnostics.Complete();
    Console.WriteLine("Focused permission governance fixture passed.");
    return;
}

if (string.Equals(
        Environment.GetEnvironmentVariable("COZO_OM_TEST_FOCUS"),
        "schema-evolution",
        StringComparison.Ordinal))
{
    HarnessDiagnostics.Start("schema evolution parity fixture");
    await SchemaEvolutionParityFixtures.RunAsync();
    HarnessDiagnostics.Complete();
    Console.WriteLine("Focused schema evolution fixture passed.");
    return;
}

if (string.Equals(
        Environment.GetEnvironmentVariable("COZO_OM_TEST_FOCUS"),
        "existential-governance",
        StringComparison.Ordinal))
{
    HarnessDiagnostics.Start("existential governance parity fixture");
    await ExistentialGovernanceParityFixtures.RunAsync();
    HarnessDiagnostics.Complete();
    Console.WriteLine("Focused existential governance fixture passed.");
    return;
}

if (string.Equals(
        Environment.GetEnvironmentVariable("COZO_OM_TEST_FOCUS"),
        "public-surface",
        StringComparison.Ordinal))
{
    HarnessDiagnostics.Start("public surface parity fixture");
    await PublicSurfaceParityFixtures.RunAsync();
    HarnessDiagnostics.Complete();
    Console.WriteLine("Focused public surface parity fixture passed.");
    return;
}

HarnessDiagnostics.Start("core OM and portable Datalog matrix");


var bindingRows = new[]
{
    new BehaviorBindingRow(
        new BehaviorBindingKey(BehaviorKind.Constraint, "PortableOwner", "portable_constraint", BehaviorCallbackSlot.When, "", -1),
        "binding:constraint:when"),
    new BehaviorBindingRow(
        new BehaviorBindingKey(BehaviorKind.Constraint, "PortableOwner", "portable_constraint", BehaviorCallbackSlot.Then, "", -1),
        "binding:constraint:then"),
    new BehaviorBindingRow(
        new BehaviorBindingKey(BehaviorKind.Constraint, "PortableOwner", "portable_constraint", BehaviorCallbackSlot.Validator, "", -1),
        "binding:constraint:validator"),
    new BehaviorBindingRow(
        new BehaviorBindingKey(BehaviorKind.ComputedProp, "PortableOwner", "portable_computed_prop", BehaviorCallbackSlot.Compute, "", -1),
        "binding:computedProp"),
    new BehaviorBindingRow(
        new BehaviorBindingKey(BehaviorKind.Operation, "PortableOwner", "portable_operation", BehaviorCallbackSlot.Handler, "", -1),
        "binding:operation"),
    new BehaviorBindingRow(
        new BehaviorBindingKey(BehaviorKind.Mutation, "PortableOwner", "portable_mutation", BehaviorCallbackSlot.Executor, "", -1),
        "binding:mutation"),
    new BehaviorBindingRow(
        new BehaviorBindingKey(BehaviorKind.Interceptor, "PortableOwner", "portable_operation", BehaviorCallbackSlot.Handler, "after", 7),
        "binding:interceptor:after:7"),
};

using (var bindingDb = new CozoDb(engine: "mem", path: ""))
{
    var bindingOm = new CozoOm(bindingDb);
    await bindingOm.InitSchemaAsync();
    foreach (var row in bindingRows)
    {
        await BehaviorBindingLogic.PutAsync(bindingOm.Runtime, row);
    }

    var listed = await BehaviorBindingLogic.ListAsync(bindingOm.Runtime);
    Assert(listed.SequenceEqual(bindingRows), "binding rows should use stable typed key ordering across all five behavior kinds");
    Assert((await BehaviorBindingLogic.QueryAsync(bindingOm.Runtime, bindingRows[^1].Key)) == bindingRows[^1],
        "interceptor binding queries should preserve the exact phase and sequence key");

    await bindingOm.WriteSchemaSnapshotAsync(901, "behavior binding baseline");
    await BehaviorBindingLogic.PutAsync(
        bindingOm.Runtime,
        bindingRows[0] with { BindingId = "binding:constraint:when:changed" });
    await BehaviorBindingLogic.PutAsync(
        bindingOm.Runtime,
        new BehaviorBindingRow(
            new BehaviorBindingKey(BehaviorKind.Interceptor, "PortableOwner", "portable_operation", BehaviorCallbackSlot.Handler, "before", 11),
            "binding:interceptor:transient"));
    await bindingOm.RollbackSchemaAsync(901, strict: true);
    Assert((await BehaviorBindingLogic.ListAsync(bindingOm.Runtime)).SequenceEqual(bindingRows),
        "schema rollback should restore binding rows exactly and remove post-snapshot rows");

    await ExpectExceptionAsync<ArgumentException>(
        () => BehaviorBindingLogic.PutAsync(
            bindingOm.Runtime,
            new BehaviorBindingRow(
                new BehaviorBindingKey(BehaviorKind.Operation, " ", "portable_operation", BehaviorCallbackSlot.Handler, "", -1),
                "binding:invalid")),
        "binding owner names should be required");
    await ExpectExceptionAsync<ArgumentException>(
        () => BehaviorBindingLogic.PutAsync(
            bindingOm.Runtime,
            new BehaviorBindingRow(
                new BehaviorBindingKey(BehaviorKind.Operation, "PortableOwner", "portable_operation", BehaviorCallbackSlot.Handler, "before", 0),
                "binding:invalid")),
        "non-interceptor bindings should require canonical empty phase and sentinel sequence");
    await ExpectExceptionAsync<ArgumentException>(
        () => BehaviorBindingLogic.PutAsync(
            bindingOm.Runtime,
            new BehaviorBindingRow(
                new BehaviorBindingKey(BehaviorKind.Interceptor, "PortableOwner", "portable_operation", BehaviorCallbackSlot.Compute, "before", 0),
                "binding:invalid")),
        "binding slots should be valid for their behavior kind");
    await ExpectExceptionAsync<ArgumentException>(
        () => BehaviorBindingLogic.PutAsync(bindingOm.Runtime, bindingRows[0] with { BindingId = " " }),
        "binding ids should be required");
}

HarnessDiagnostics.Start("behavior catalog and manifest matrix");

using (var catalogDb = new CozoDb(engine: "mem", path: ""))
{
    var catalogOm = new CozoOm(catalogDb);
    await catalogOm.InitSchemaAsync();
    await catalogOm.DefineClassAsync("PortableOwner", "Portable behavior owner");
    await catalogOm.DefineConstraintAsync("PortableOwner", "portable_constraint", "custom", "portable constraint message");
    await catalogOm.DefineComputedPropAsync("PortableOwner", "portable_computed_prop", "portable computedProp description");
    await catalogOm.DefineOperationAsync("PortableOwner", "portable_operation", "portable operation description");
    await catalogOm.DefineOperationAsync("PortableOwner", "native_operation", "native operation description");
    await catalogOm.DefineMutationAsync("PortableOwner", "portable_mutation", "portable mutation description");
    await catalogOm.AddInterceptorAsync("PortableOwner", "portable_operation", "after", 7, "portable interceptor description");
    foreach (var row in bindingRows)
    {
        await BehaviorBindingLogic.PutAsync(catalogOm.Runtime, row);
    }

    var unresolvedCatalog = await catalogOm.GetBehaviorCatalogAsync();
    var boundCallbacks = unresolvedCatalog.Behaviors.SelectMany(entry => entry.Callbacks).Where(callback => callback.BindingId is not null).ToArray();
    Assert(boundCallbacks.Length == bindingRows.Length && boundCallbacks.All(callback => callback.Readiness == BehaviorReadiness.Unresolved),
        "persisted callback identities should project unresolved while the current registry is empty");
    Assert(unresolvedCatalog.Behaviors.Select(entry => entry.Kind).Distinct().Count() == 5,
        "the public behavior catalog should represent all five behavior kinds");
    var constraintEntry = unresolvedCatalog.Behaviors.Single(entry =>
        entry.Kind == BehaviorCatalogKind.Constraint && entry.OwnerClass == "PortableOwner" && entry.Name == "portable_constraint");
    Assert(constraintEntry.ConstraintKind == "custom"
           && constraintEntry.Message == "portable constraint message"
           && constraintEntry.Callbacks.Select(callback => callback.Slot).SequenceEqual(
               new[] { BehaviorCatalogCallbackSlot.When, BehaviorCatalogCallbackSlot.Then, BehaviorCatalogCallbackSlot.Validator }),
        "constraint catalog entries should preserve metadata and expose when, then, and validator slots");
    var interceptorEntry = unresolvedCatalog.Behaviors.Single(entry => entry.Kind == BehaviorCatalogKind.Interceptor);
    Assert(interceptorEntry.InterceptorPhase == "after" && interceptorEntry.InterceptorSeq == 7
           && interceptorEntry.Description == "portable interceptor description",
        "interceptor catalog entries should preserve exact phase, sequence, and description");
    var catalogJson = JsonSerializer.Serialize(unresolvedCatalog);
    Assert(!catalogJson.Contains("System.Func", StringComparison.Ordinal)
           && !typeof(BehaviorCatalogEntry).GetProperties().Any(property => typeof(Delegate).IsAssignableFrom(property.PropertyType)),
        "the public behavior catalog should be serializable data without delegates");

    var manifestBytes = await catalogOm.ExportBehaviorManifestJsonAsync();
    var repeatedManifestBytes = await catalogOm.ExportBehaviorManifestJsonAsync();
    Assert(manifestBytes.SequenceEqual(repeatedManifestBytes),
        "exporting the same behavior catalog repeatedly should be byte-for-byte stable");
    var manifestJson = System.Text.Encoding.UTF8.GetString(manifestBytes);
    Assert(manifestJson.Contains("\"version\":1", StringComparison.Ordinal)
           && manifestJson.Contains("\"kind\":\"constraint\"", StringComparison.Ordinal)
           && manifestJson.Contains("\"slot\":\"validator\"", StringComparison.Ordinal)
           && manifestJson.Contains("\"readiness\":\"unresolved\"", StringComparison.Ordinal),
        "the canonical behavior manifest should use version 1 and explicit lowercase wire values");
    var decodedManifest = BehaviorManifestJsonCodec.Decode(manifestJson);
    Assert(decodedManifest.Success && decodedManifest.Catalog is not null && decodedManifest.Diagnostics.IsEmpty,
        "a canonical behavior manifest should decode without diagnostics");
    Assert(BehaviorManifestJsonCodec.Encode(decodedManifest.Catalog!).SequenceEqual(manifestBytes),
        "canonical behavior manifest decode and re-encode should preserve semantic bytes for all kinds and slots");
    var reversedCatalog = new BehaviorCatalog(unresolvedCatalog.Behaviors
        .Reverse()
        .Select(entry => entry with { Callbacks = entry.Callbacks.Reverse().ToImmutableArray() })
        .ToArray());
    Assert(BehaviorManifestJsonCodec.Encode(reversedCatalog).SequenceEqual(manifestBytes),
        "canonical behavior manifest ordering should not depend on caller collection order");
    using (var canceledExport = new CancellationTokenSource())
    {
        canceledExport.Cancel();
        await ExpectExceptionAsync<OperationCanceledException>(
            () => catalogOm.ExportBehaviorManifestJsonAsync(canceledExport.Token),
            "behavior manifest export should honor cancellation before reading the catalog");
    }

    const string invalidManifest = """
    {
      "version": 2,
      "behaviors": [
        {
          "kind": "mystery",
          "ownerClass": " ",
          "name": "bad",
          "constraintKind": null,
          "message": null,
          "description": null,
          "interceptorPhase": null,
          "interceptorSeq": null,
          "callbacks": [{ "slot": "mystery-slot", "bindingId": "binding:a", "readiness": "ready" }]
        },
        {
          "kind": "operation",
          "ownerClass": "PortableOwner",
          "name": "duplicate",
          "constraintKind": null,
          "message": null,
          "description": "first",
          "interceptorPhase": null,
          "interceptorSeq": null,
          "callbacks": [
            { "slot": "handler", "bindingId": "binding:a", "readiness": "unresolved" },
            { "slot": "handler", "bindingId": "binding:b", "readiness": "ready" }
          ]
        },
        {
          "kind": "operation",
          "ownerClass": "PortableOwner",
          "name": "duplicate",
          "constraintKind": null,
          "message": null,
          "description": "conflict",
          "interceptorPhase": null,
          "interceptorSeq": null,
          "callbacks": [{ "slot": "compute", "bindingId": "binding:c", "readiness": "unresolved" }]
        }
      ]
    }
    """;
    var invalidDecode = BehaviorManifestJsonCodec.Decode(invalidManifest);
    Assert(!invalidDecode.Success && invalidDecode.Catalog is null,
        "invalid behavior manifests should return diagnostics without a partial catalog");
    Assert(new[] { "OMM1002", "OMM1101", "OMM1102", "OMM1201", "OMM1301", "OMM1302" }
            .All(code => invalidDecode.Diagnostics.Any(diagnostic => diagnostic.Code == code)),
        "unknown version, kind, slot, invalid keys, duplicate bindings, and conflicting behavior keys should be diagnosed");
    Assert(invalidDecode.Diagnostics.SequenceEqual(invalidDecode.Diagnostics
            .OrderBy(diagnostic => diagnostic.Code, StringComparer.Ordinal)
            .ThenBy(diagnostic => diagnostic.Path, StringComparer.Ordinal)
            .ThenBy(diagnostic => diagnostic.Message, StringComparer.Ordinal)),
        "manifest diagnostics should have deterministic code/path/message ordering");
    var malformedDecode = BehaviorManifestJsonCodec.Decode("{ not-json");
    Assert(!malformedDecode.Success
           && malformedDecode.Diagnostics.Length == 1
           && malformedDecode.Diagnostics[0].Code == "OMM1000",
        "malformed JSON should return one structured parse diagnostic instead of leaking JsonException");
    var wrongScalarTypes = BehaviorManifestJsonCodec.Decode("{\"version\":\"1\",\"behaviors\":[]}");
    Assert(!wrongScalarTypes.Success && wrongScalarTypes.Diagnostics.Single().Code == "OMM1001",
        "wrong JSON scalar types should return schema diagnostics instead of throwing runtime exceptions");

    const string invalidMetadataMatrixManifest = """
        {
          "version": 1,
          "behaviors": [
            { "kind": "constraint", "ownerClass": "PortableOwner", "name": "invalid_constraint", "constraintKind": null, "message": "allowed", "description": "forbidden", "interceptorPhase": "before", "interceptorSeq": 0, "callbacks": [] },
            { "kind": "computedProp", "ownerClass": "PortableOwner", "name": "invalid_computed", "constraintKind": "custom", "message": "forbidden", "description": "allowed", "interceptorPhase": null, "interceptorSeq": null, "callbacks": [] },
            { "kind": "operation", "ownerClass": "PortableOwner", "name": "invalid_operation", "constraintKind": "custom", "message": "forbidden", "description": "allowed", "interceptorPhase": null, "interceptorSeq": null, "callbacks": [] },
            { "kind": "mutation", "ownerClass": "PortableOwner", "name": "invalid_mutation", "constraintKind": "custom", "message": "forbidden", "description": "allowed", "interceptorPhase": "after", "interceptorSeq": 2, "callbacks": [] },
            { "kind": "interceptor", "ownerClass": "PortableOwner", "name": "invalid_interceptor", "constraintKind": "custom", "message": "forbidden", "description": "allowed", "interceptorPhase": "after", "interceptorSeq": 3, "callbacks": [] }
          ]
        }
        """;
    var invalidMetadataMatrixDecode = BehaviorManifestJsonCodec.Decode(invalidMetadataMatrixManifest);
    var expectedMetadataMatrixDiagnostics = new[]
    {
        new BehaviorManifestDiagnostic("OMM1203", "$.behaviors[0].constraintKind", "Property 'constraintKind' must be a string for behavior kind 'constraint'."),
        new BehaviorManifestDiagnostic("OMM1203", "$.behaviors[0].description", "Property 'description' must be null for behavior kind 'constraint'."),
        new BehaviorManifestDiagnostic("OMM1203", "$.behaviors[0].interceptorPhase", "Property 'interceptorPhase' must be null for behavior kind 'constraint'."),
        new BehaviorManifestDiagnostic("OMM1203", "$.behaviors[0].interceptorSeq", "Property 'interceptorSeq' must be null for behavior kind 'constraint'."),
        new BehaviorManifestDiagnostic("OMM1203", "$.behaviors[1].constraintKind", "Property 'constraintKind' must be null for behavior kind 'computedProp'."),
        new BehaviorManifestDiagnostic("OMM1203", "$.behaviors[1].message", "Property 'message' must be null for behavior kind 'computedProp'."),
        new BehaviorManifestDiagnostic("OMM1203", "$.behaviors[2].constraintKind", "Property 'constraintKind' must be null for behavior kind 'operation'."),
        new BehaviorManifestDiagnostic("OMM1203", "$.behaviors[2].message", "Property 'message' must be null for behavior kind 'operation'."),
        new BehaviorManifestDiagnostic("OMM1203", "$.behaviors[3].constraintKind", "Property 'constraintKind' must be null for behavior kind 'mutation'."),
        new BehaviorManifestDiagnostic("OMM1203", "$.behaviors[3].interceptorPhase", "Property 'interceptorPhase' must be null for behavior kind 'mutation'."),
        new BehaviorManifestDiagnostic("OMM1203", "$.behaviors[3].interceptorSeq", "Property 'interceptorSeq' must be null for behavior kind 'mutation'."),
        new BehaviorManifestDiagnostic("OMM1203", "$.behaviors[3].message", "Property 'message' must be null for behavior kind 'mutation'."),
        new BehaviorManifestDiagnostic("OMM1203", "$.behaviors[4].constraintKind", "Property 'constraintKind' must be null for behavior kind 'interceptor'."),
        new BehaviorManifestDiagnostic("OMM1203", "$.behaviors[4].message", "Property 'message' must be null for behavior kind 'interceptor'."),
    };
    Assert(!invalidMetadataMatrixDecode.Success
           && invalidMetadataMatrixDecode.Catalog is null
           && invalidMetadataMatrixDecode.Diagnostics.SequenceEqual(expectedMetadataMatrixDiagnostics),
        "canonical decode must reject every cross-kind metadata field with exact deterministic diagnostics");
    var invalidInterceptorKeyDecode = BehaviorManifestJsonCodec.Decode("""
        {"version":1,"behaviors":[{"kind":"interceptor","ownerClass":"PortableOwner","name":"invalid_interceptor_key","constraintKind":null,"message":null,"description":"allowed","interceptorPhase":"during","interceptorSeq":-1,"callbacks":[]}]}
        """);
    Assert(!invalidInterceptorKeyDecode.Success
           && invalidInterceptorKeyDecode.Diagnostics.SequenceEqual(
           [
               new BehaviorManifestDiagnostic(
                   "OMM1201",
                   "$.behaviors[0]",
                   "Interceptor keys require phase before/after and a non-negative sequence."),
           ]),
        "interceptor phase and sequence must retain their existing canonical key rule");

    var mutableCallbacks = new List<BehaviorCallbackBinding>
    {
        new(BehaviorCatalogCallbackSlot.Handler, "binding:immutable", BehaviorReadiness.Unresolved),
    };
    var immutableEntry = new BehaviorCatalogEntry(
        BehaviorCatalogKind.Operation,
        "ImmutableOwner",
        "immutable_operation",
        null,
        null,
        "immutable",
        null,
        null,
        mutableCallbacks);
    var mutableBehaviors = new List<BehaviorCatalogEntry> { immutableEntry };
    var immutableCatalog = new BehaviorCatalog(mutableBehaviors);
    var mutableDiagnostics = new List<BehaviorManifestDiagnostic> { new("OMM9999", "$", "immutable") };
    var immutableDecode = new BehaviorManifestDecodeResult(null, mutableDiagnostics);
    mutableCallbacks[0] = mutableCallbacks[0] with { BindingId = "binding:mutated" };
    mutableBehaviors.Clear();
    mutableDiagnostics.Clear();
    Assert(immutableCatalog.Behaviors.Length == 1
           && immutableCatalog.Behaviors[0].Callbacks[0].BindingId == "binding:immutable"
           && immutableDecode.Diagnostics.Length == 1,
        "catalog, callback, and diagnostic constructors should defensively copy mutable source collections");
    Assert(((ICollection<BehaviorCatalogEntry>)immutableCatalog.Behaviors).IsReadOnly
           && ((ICollection<BehaviorCallbackBinding>)immutableEntry.Callbacks).IsReadOnly
           && ((ICollection<BehaviorManifestDiagnostic>)immutableDecode.Diagnostics).IsReadOnly,
        "public behavior collections should remain read-only even after interface downcasts");
    Assert(RejectsImmutableArrayMutation(immutableCatalog.Behaviors, immutableEntry with { Name = "mutated" })
           && RejectsImmutableArrayMutation(
               immutableEntry.Callbacks,
               immutableEntry.Callbacks[0] with { BindingId = "binding:mutated" })
           && RejectsImmutableArrayMutation(
               immutableDecode.Diagnostics,
               immutableDecode.Diagnostics[0] with { Message = "mutated" })
           && immutableCatalog.Behaviors[0].Name == "immutable_operation"
           && immutableCatalog.Behaviors[0].Callbacks[0].BindingId == "binding:immutable"
           && immutableDecode.Diagnostics[0].Message == "immutable",
        "downcast mutation attempts must be rejected without changing public behavior values");

    catalogOm.Runtime.Registry.RegisterConstraint("PortableOwner", "portable_constraint", _ => ValueTask.FromResult(true), _ => ValueTask.FromResult(true));
    catalogOm.Runtime.Registry.RegisterValidator("PortableOwner", "portable_constraint", _ => ValueTask.FromResult<string?>(null));
    catalogOm.Runtime.Registry.RegisterComputedProp("PortableOwner", "portable_computed_prop", _ => ValueTask.FromResult<object?>(1));
    catalogOm.Runtime.Registry.RegisterOperation("PortableOwner", "portable_operation", (_, _) => ValueTask.FromResult<IReadOnlyList<MutationSpec>>([]));
    catalogOm.Runtime.Registry.RegisterMutation("PortableOwner", "portable_mutation", (_, _) => ValueTask.CompletedTask);
    catalogOm.Runtime.Registry.RegisterInterceptor("PortableOwner", "portable_operation", "after", 7, _ => ValueTask.CompletedTask);
    catalogOm.Runtime.Registry.RegisterOperation("PortableOwner", "native_operation", (_, _) => ValueTask.FromResult<IReadOnlyList<MutationSpec>>([]));

    var legacyCatalog = await catalogOm.GetBehaviorCatalogAsync();
    Assert(legacyCatalog.Behaviors.SelectMany(entry => entry.Callbacks)
            .Where(callback => callback.BindingId is not null)
            .All(callback => callback.Readiness == BehaviorReadiness.Unresolved),
        "legacy registrations without portable binding identities must not satisfy persisted bindings");

    catalogOm.Runtime.Registry.RegisterConstraint(
        "PortableOwner",
        "portable_constraint",
        "binding:constraint:when",
        _ => ValueTask.FromResult(true),
        "binding:constraint:then:mismatch",
        _ => ValueTask.FromResult(true));
    catalogOm.Runtime.Registry.RegisterValidator(
        "PortableOwner",
        "portable_constraint",
        "binding:constraint:validator:mismatch",
        _ => ValueTask.FromResult<string?>(null));
    catalogOm.Runtime.Registry.RegisterComputedProp("PortableOwner", "portable_computed_prop", "binding:computedProp:mismatch", _ => ValueTask.FromResult<object?>(1));
    catalogOm.Runtime.Registry.RegisterOperation("PortableOwner", "portable_operation", "binding:operation:mismatch", (_, _) => ValueTask.FromResult<IReadOnlyList<MutationSpec>>([]));
    catalogOm.Runtime.Registry.RegisterMutation("PortableOwner", "portable_mutation", "binding:mutation:mismatch", (_, _) => ValueTask.CompletedTask);
    catalogOm.Runtime.Registry.RegisterInterceptor("PortableOwner", "portable_operation", "after", 7, "binding:interceptor:mismatch", _ => ValueTask.CompletedTask);
    var mismatchedCatalog = await catalogOm.GetBehaviorCatalogAsync();
    var mismatchedConstraint = mismatchedCatalog.Behaviors.Single(entry => entry.Kind == BehaviorCatalogKind.Constraint);
    Assert(mismatchedConstraint.Callbacks.Single(callback => callback.Slot == BehaviorCatalogCallbackSlot.When).Readiness == BehaviorReadiness.Ready
           && mismatchedConstraint.Callbacks.Single(callback => callback.Slot == BehaviorCatalogCallbackSlot.Then).Readiness == BehaviorReadiness.Unresolved
           && mismatchedConstraint.Callbacks.Single(callback => callback.Slot == BehaviorCatalogCallbackSlot.Validator).Readiness == BehaviorReadiness.Unresolved,
        "constraint when and then identities should be matched independently and mismatches should remain unresolved");
    Assert(mismatchedCatalog.Behaviors.SelectMany(entry => entry.Callbacks)
            .Where(callback => callback.Slot != BehaviorCatalogCallbackSlot.When && callback.BindingId is not null)
            .All(callback => callback.Readiness == BehaviorReadiness.Unresolved),
        "a callback registered at the same key with a different binding id must remain unresolved");

    catalogOm.Runtime.Registry.RegisterConstraint(
        "PortableOwner",
        "portable_constraint",
        "binding:constraint:when",
        _ => ValueTask.FromResult(true),
        "binding:constraint:then",
        _ => ValueTask.FromResult(true));
    catalogOm.Runtime.Registry.RegisterValidator("PortableOwner", "portable_constraint", "binding:constraint:validator", _ => ValueTask.FromResult<string?>(null));
    catalogOm.Runtime.Registry.RegisterComputedProp("PortableOwner", "portable_computed_prop", "binding:computedProp", _ => ValueTask.FromResult<object?>(1));
    catalogOm.Runtime.Registry.RegisterOperation("PortableOwner", "portable_operation", "binding:operation", (_, _) => ValueTask.FromResult<IReadOnlyList<MutationSpec>>([]));
    catalogOm.Runtime.Registry.RegisterMutation("PortableOwner", "portable_mutation", "binding:mutation", (_, _) => ValueTask.CompletedTask);
    catalogOm.Runtime.Registry.RegisterInterceptor("PortableOwner", "portable_operation", "after", 7, "binding:interceptor:after:7", _ => ValueTask.CompletedTask);

    var readyCatalog = await catalogOm.GetBehaviorCatalogAsync();
    Assert(readyCatalog.Behaviors.SelectMany(entry => entry.Callbacks)
            .Where(callback => callback.BindingId is not null)
            .All(callback => callback.Readiness == BehaviorReadiness.Ready),
        "only exact portable binding identities should project the catalog ready");
    Assert(readyCatalog.Behaviors.Single(entry => entry.Kind == BehaviorCatalogKind.Operation && entry.Name == "native_operation")
            .Callbacks.Single().Readiness == BehaviorReadiness.Unbound,
        "a native definition without a persistent binding identity must remain unbound even when a callback is registered");

    await catalogOm.DefineOperationAsync("PortableOwner", "sparse_operation", "sparse operation");
    await catalogOm.AddInterceptorAsync("PortableOwner", "sparse_operation", "before", 7, "sparse existing");
    catalogOm.Runtime.Registry.RegisterInterceptor("PortableOwner", "sparse_operation", "before", 7, _ => ValueTask.CompletedTask);
    await catalogOm.AddInterceptorAsync("PortableOwner", "sparse_operation", "before", _ => ValueTask.CompletedTask, "sparse appended");
    var sparseInterceptors = (await catalogOm.GetBehaviorCatalogAsync()).Behaviors
        .Where(entry => entry.Kind == BehaviorCatalogKind.Interceptor && entry.Name == "sparse_operation" && entry.InterceptorPhase == "before")
        .ToArray();
    Assert(sparseInterceptors.Select(entry => entry.InterceptorSeq).SequenceEqual(new int?[] { 7, 8 })
           && catalogOm.Runtime.Registry.TryGetInterceptor("PortableOwner", "sparse_operation", "before", 7, out _)
           && catalogOm.Runtime.Registry.TryGetInterceptor("PortableOwner", "sparse_operation", "before", 8, out _),
        "sparse interceptor allocation should use max sequence plus one without overwriting the existing item");
}

using (var snapshotDb = new CozoDb(engine: "mem", path: ""))
{
    var snapshotStore = new RunHookOmStore(new CozoDbOmStore(snapshotDb));
    var snapshotOm = new CozoOm(snapshotStore);
    await snapshotOm.InitSchemaAsync();
    await snapshotOm.DefineClassAsync("SnapshotOwner", "Snapshot owner");
    await snapshotOm.DefineOperationAsync("SnapshotOwner", "snapshot_operation", "snapshot operation");
    await BehaviorBindingLogic.PutAsync(
        snapshotOm.Runtime,
        new BehaviorBindingRow(
            new BehaviorBindingKey(BehaviorKind.Operation, "SnapshotOwner", "snapshot_operation", BehaviorCallbackSlot.Handler, "", -1),
            "binding:snapshot:operation"));
    snapshotStore.RunOnceWhenScriptContains = "*om_behavior_binding";
    snapshotStore.OnRunOnce = () => snapshotOm.Runtime.Registry.RegisterOperation(
        "SnapshotOwner",
        "snapshot_operation",
        "binding:snapshot:operation",
        (_, _) => ValueTask.FromResult<IReadOnlyList<MutationSpec>>([]));

    var capturedCatalog = await snapshotOm.GetBehaviorCatalogAsync();
    Assert(capturedCatalog.Behaviors.Single().Callbacks.Single().Readiness == BehaviorReadiness.Unresolved,
        "one catalog read should use the registry snapshot captured before asynchronous metadata reads");
    Assert((await snapshotOm.GetBehaviorCatalogAsync()).Behaviors.Single().Callbacks.Single().Readiness == BehaviorReadiness.Ready,
        "a later catalog read should observe the atomically published registry snapshot");
}

HarnessDiagnostics.Start("behavior schema snapshots and legacy compatibility");

using (var legacyDb = new CozoDb(engine: "mem", path: ""))
{
    legacyDb.Run(":create om_class_def {class_name => description, parent_class}");
    legacyDb.Run(
        "?[class_name, description, parent_class] <- [[$class_name, $description, null]]\n:put om_class_def {class_name => description, parent_class}",
        new { class_name = "LegacyClass", description = "legacy row" });
    var legacyOm = new CozoOm(legacyDb);
    await legacyOm.InitSchemaAsync();
    using var legacyRows = legacyDb.Run("?[description] := *om_class_def{class_name: \"LegacyClass\", description, parent_class: _parent}");
    Assert(Rows(legacyRows).GetArrayLength() == 1 && Rows(legacyRows)[0][0].GetString() == "legacy row",
        "additive schema initialization should preserve rows in a database created before the binding relation");
    await BehaviorBindingLogic.PutAsync(legacyOm.Runtime, bindingRows[0]);
    Assert((await BehaviorBindingLogic.ListAsync(legacyOm.Runtime)).Count == 1,
        "additive schema initialization should create the behavior binding relation for legacy databases");
}

var persistenceDirectory = Path.Combine(Path.GetTempPath(), $"cozo-om-binding-{Guid.NewGuid():N}");
Directory.CreateDirectory(persistenceDirectory);
var persistencePath = Path.Combine(persistenceDirectory, "bindings.db");
try
{
    using (var persistentDb = new CozoDb(engine: "sqlite", path: persistencePath))
    {
        var persistentOm = new CozoOm(persistentDb);
        await persistentOm.InitSchemaAsync();
        await persistentOm.DefineClassAsync("PortableOwner", "Portable behavior owner");
        await persistentOm.DefineConstraintAsync("PortableOwner", "portable_constraint", "custom", "portable constraint message");
        await persistentOm.DefineComputedPropAsync("PortableOwner", "portable_computed_prop", "portable computedProp description");
        await persistentOm.DefineOperationAsync("PortableOwner", "portable_operation", "portable operation description");
        await persistentOm.DefineMutationAsync("PortableOwner", "portable_mutation", "portable mutation description");
        await persistentOm.AddInterceptorAsync("PortableOwner", "portable_operation", "after", 7, "portable interceptor description");
        foreach (var row in bindingRows)
        {
            await BehaviorBindingLogic.PutAsync(persistentOm.Runtime, row);
        }

        persistentOm.Runtime.Registry.RegisterConstraint("PortableOwner", "portable_constraint", "binding:constraint:when", _ => ValueTask.FromResult(true), "binding:constraint:then", _ => ValueTask.FromResult(true));
        persistentOm.Runtime.Registry.RegisterValidator("PortableOwner", "portable_constraint", "binding:constraint:validator", _ => ValueTask.FromResult<string?>(null));
        persistentOm.Runtime.Registry.RegisterComputedProp("PortableOwner", "portable_computed_prop", "binding:computedProp", _ => ValueTask.FromResult<object?>(1));
        persistentOm.Runtime.Registry.RegisterOperation("PortableOwner", "portable_operation", "binding:operation", (_, _) => ValueTask.FromResult<IReadOnlyList<MutationSpec>>([]));
        persistentOm.Runtime.Registry.RegisterMutation("PortableOwner", "portable_mutation", "binding:mutation", (_, _) => ValueTask.CompletedTask);
        persistentOm.Runtime.Registry.RegisterInterceptor("PortableOwner", "portable_operation", "after", 7, "binding:interceptor:after:7", _ => ValueTask.CompletedTask);
        Assert((await persistentOm.GetBehaviorCatalogAsync()).Behaviors.SelectMany(entry => entry.Callbacks)
                .Where(callback => callback.BindingId is not null)
                .All(callback => callback.Readiness == BehaviorReadiness.Ready),
            "persisted bindings should project ready before the process closes while callbacks are registered");
    }

    using (var reopenedDb = new CozoDb(engine: "sqlite", path: persistencePath))
    {
        var reopenedOm = new CozoOm(reopenedDb);
        await reopenedOm.InitSchemaAsync();
        Assert((await BehaviorBindingLogic.ListAsync(reopenedOm.Runtime)).SequenceEqual(bindingRows),
            "binding identities should survive closing and reopening the persistent database");
        var restartedCatalog = await reopenedOm.GetBehaviorCatalogAsync();
        Assert(restartedCatalog.Behaviors.SelectMany(entry => entry.Callbacks)
                .Where(callback => callback.BindingId is not null)
                .All(callback => callback.Readiness == BehaviorReadiness.Unresolved),
            "a restarted process should preserve binding ids while projecting every callback unresolved");

        reopenedOm.Runtime.Registry.RegisterConstraint("PortableOwner", "portable_constraint", "binding:constraint:when", _ => ValueTask.FromResult(true), "binding:constraint:then", _ => ValueTask.FromResult(true));
        reopenedOm.Runtime.Registry.RegisterValidator("PortableOwner", "portable_constraint", "binding:constraint:validator", _ => ValueTask.FromResult<string?>(null));
        reopenedOm.Runtime.Registry.RegisterComputedProp("PortableOwner", "portable_computed_prop", "binding:computedProp", _ => ValueTask.FromResult<object?>(1));
        reopenedOm.Runtime.Registry.RegisterOperation("PortableOwner", "portable_operation", "binding:operation", (_, _) => ValueTask.FromResult<IReadOnlyList<MutationSpec>>([]));
        reopenedOm.Runtime.Registry.RegisterMutation("PortableOwner", "portable_mutation", "binding:mutation", (_, _) => ValueTask.CompletedTask);
        reopenedOm.Runtime.Registry.RegisterInterceptor("PortableOwner", "portable_operation", "after", 7, "binding:interceptor:after:7", _ => ValueTask.CompletedTask);
        Assert((await reopenedOm.GetBehaviorCatalogAsync()).Behaviors.SelectMany(entry => entry.Callbacks)
                .Where(callback => callback.BindingId is not null)
                .All(callback => callback.Readiness == BehaviorReadiness.Ready),
            "rebinding the existing typed registry callbacks should project all persisted bindings ready");
    }
}
finally
{
    Directory.Delete(persistenceDirectory, recursive: true);
}

HarnessDiagnostics.Start("behavior readiness and registry matrix");

using (var readinessDb = new CozoDb(engine: "mem", path: ""))
{
    var readinessOm = new CozoOm(readinessDb);
    await readinessOm.InitSchemaAsync();

    foreach (var className in new[]
             {
                 "ReadinessBase", "ReadinessChild", "WhenOwner", "ThenOwner", "ValidatorOwner", "ComputedOwner",
                 "ComputedStoredBase", "ComputedReadyOwner", "ComputedUnboundOwner"
             })
    {
        await readinessOm.DefineClassAsync(
            className,
            className,
            className == "ReadinessChild" ? "ReadinessBase" : null);
    }

    await readinessOm.DefineFieldAsync("ReadinessBase", "effect", OmValueType.String);
    await readinessOm.CreateObjectAsync("ready:child", "ReadinessChild", "Readiness child");
    await readinessOm.SetFieldValueAsync("ready:child", "effect", "initial");

    await readinessOm.DefineConstraintAsync("WhenOwner", "when_gap", "conditional", "when gap");
    await readinessOm.CreateObjectAsync("ready:when", "WhenOwner", "When owner");
    await BehaviorBindingLogic.PutAsync(
        readinessOm.Runtime,
        new BehaviorBindingRow(
            new BehaviorBindingKey(BehaviorKind.Constraint, "WhenOwner", "when_gap", BehaviorCallbackSlot.When, "", -1),
            "binding:when"));
    await ExpectUnresolvedAsync(
        async () => { await readinessOm.ValidateObjectAsync("ready:when"); },
        BehaviorCatalogKind.Constraint,
        "WhenOwner",
        "constraint:WhenOwner/when_gap",
        BehaviorCatalogCallbackSlot.When,
        "binding:when");
    await ExpectUnresolvedAsync(
        async () => { await ConstraintLogic.ValidateConstraintsAsync(readinessOm.Runtime, "ready:when", ["conditional"]); },
        BehaviorCatalogKind.Constraint,
        "WhenOwner",
        "constraint:WhenOwner/when_gap",
        BehaviorCatalogCallbackSlot.When,
        "binding:when");

    await readinessOm.DefineConstraintAsync("ThenOwner", "then_gap", "conditional", "then gap");
    await readinessOm.CreateObjectAsync("ready:then", "ThenOwner", "Then owner");
    await BehaviorBindingLogic.PutAsync(
        readinessOm.Runtime,
        new BehaviorBindingRow(
            new BehaviorBindingKey(BehaviorKind.Constraint, "ThenOwner", "then_gap", BehaviorCallbackSlot.When, "", -1),
            "binding:then:when"));
    await BehaviorBindingLogic.PutAsync(
        readinessOm.Runtime,
        new BehaviorBindingRow(
            new BehaviorBindingKey(BehaviorKind.Constraint, "ThenOwner", "then_gap", BehaviorCallbackSlot.Then, "", -1),
            "binding:then"));
    readinessOm.Runtime.Registry.RegisterConstraint(
        "ThenOwner",
        "then_gap",
        "binding:then:when",
        _ => ValueTask.FromResult(true),
        "binding:then:mismatch",
        _ => ValueTask.FromResult(true));
    await ExpectUnresolvedAsync(
        async () => { await readinessOm.ValidateObjectAsync("ready:then"); },
        BehaviorCatalogKind.Constraint,
        "ThenOwner",
        "constraint:ThenOwner/then_gap",
        BehaviorCatalogCallbackSlot.Then,
        "binding:then");

    await readinessOm.DefineConstraintAsync("ValidatorOwner", "validator_gap", "custom", "validator gap");
    await readinessOm.CreateObjectAsync("ready:validator", "ValidatorOwner", "Validator owner");
    await BehaviorBindingLogic.PutAsync(
        readinessOm.Runtime,
        new BehaviorBindingRow(
            new BehaviorBindingKey(BehaviorKind.Constraint, "ValidatorOwner", "validator_gap", BehaviorCallbackSlot.Validator, "", -1),
            "binding:validator"));
    await ExpectUnresolvedAsync(
        async () => { await readinessOm.ValidateObjectAsync("ready:validator"); },
        BehaviorCatalogKind.Constraint,
        "ValidatorOwner",
        "constraint:ValidatorOwner/validator_gap",
        BehaviorCatalogCallbackSlot.Validator,
        "binding:validator");

    await readinessOm.DefineComputedPropAsync("ComputedOwner", "computed_prop_gap");
    await readinessOm.CreateObjectAsync("ready:computedProp", "ComputedOwner", "ComputedProp owner");
    await BehaviorBindingLogic.PutAsync(
        readinessOm.Runtime,
        new BehaviorBindingRow(
            new BehaviorBindingKey(BehaviorKind.ComputedProp, "ComputedOwner", "computed_prop_gap", BehaviorCallbackSlot.Compute, "", -1),
            "binding:computedProp:gap"));
    readinessOm.Runtime.Registry.RegisterComputedProp(
        "ComputedOwner",
        "computed_prop_gap",
        "binding:computedProp:mismatch",
        _ => ValueTask.FromResult<object?>(42));
    foreach (var read in new Func<Task>[]
             {
                 async () => { await readinessOm.GetFieldValueAsync("ready:computedProp", "computed_prop_gap"); },
                 async () => { await readinessOm.GetObjectViewAsync("ready:computedProp"); },
                 async () => { await readinessOm.GetFieldValueAsOfAsync("ready:computedProp", "computed_prop_gap", "2026-01-01T00:00:00Z"); },
                 async () => { await readinessOm.GetObjectViewAsOfAsync("ready:computedProp", "2026-01-01T00:00:00Z"); },
             })
    {
        await ExpectUnresolvedAsync(
            read,
            BehaviorCatalogKind.ComputedProp,
            "ComputedOwner",
            "computedProp:ComputedOwner/computed_prop_gap",
            BehaviorCatalogCallbackSlot.Compute,
            "binding:computedProp:gap");
    }

    await readinessOm.DefineClassAsync("ComputedStoredChild", "ComputedProp stored child", "ComputedStoredBase");
    await readinessOm.DefineFieldAsync("ComputedStoredBase", "stored_computed_prop", OmValueType.String);
    await readinessOm.DefineComputedPropAsync("ComputedStoredBase", "stored_computed_prop");
    await readinessOm.CreateObjectAsync("ready:computedProp:stored", "ComputedStoredChild", "ComputedProp stored child");
    await readinessOm.SetFieldValueAsync(
        "ready:computedProp:stored",
        "stored_computed_prop",
        "stored",
        new WriteOptions(ValidTime: "2025-01-01T00:00:00Z"));
    await BehaviorBindingLogic.PutAsync(
        readinessOm.Runtime,
        new BehaviorBindingRow(
            new BehaviorBindingKey(BehaviorKind.ComputedProp, "ComputedStoredBase", "stored_computed_prop", BehaviorCallbackSlot.Compute, "", -1),
            "binding:computedProp:stored"));
    var unresolvedStoredCallbackCount = 0;
    readinessOm.Runtime.Registry.RegisterComputedProp(
        "ComputedStoredBase",
        "stored_computed_prop",
        "binding:computedProp:stored:mismatch",
        _ =>
        {
            unresolvedStoredCallbackCount++;
            return ValueTask.FromResult<object?>("computedProp");
        });
    foreach (var read in new Func<Task>[]
             {
                 async () => { await readinessOm.GetFieldValueAsync("ready:computedProp:stored", "stored_computed_prop"); },
                 async () => { await readinessOm.GetObjectViewAsync("ready:computedProp:stored"); },
                 async () => { await readinessOm.GetFieldValueAsOfAsync("ready:computedProp:stored", "stored_computed_prop", "2026-01-01T00:00:00Z"); },
                 async () => { await readinessOm.GetObjectViewAsOfAsync("ready:computedProp:stored", "2026-01-01T00:00:00Z"); },
             })
    {
        await ExpectUnresolvedAsync(
            read,
            BehaviorCatalogKind.ComputedProp,
            "ComputedStoredBase",
            "computedProp:ComputedStoredBase/stored_computed_prop",
            BehaviorCatalogCallbackSlot.Compute,
            "binding:computedProp:stored");
    }
    Assert(unresolvedStoredCallbackCount == 0,
        "stored values must not invoke an unresolved inherited computedProp callback");

    await readinessOm.DefineFieldAsync("ComputedReadyOwner", "ready_computed_prop", OmValueType.String);
    await readinessOm.DefineComputedPropAsync("ComputedReadyOwner", "ready_computed_prop");
    await readinessOm.CreateObjectAsync("ready:computedProp:ready:stored", "ComputedReadyOwner", "Ready computedProp stored");
    await readinessOm.CreateObjectAsync("ready:computedProp:ready:missing", "ComputedReadyOwner", "Ready computedProp missing");
    await readinessOm.SetFieldValueAsync(
        "ready:computedProp:ready:stored",
        "ready_computed_prop",
        "stored-ready",
        new WriteOptions(ValidTime: "2025-01-01T00:00:00Z"));
    await BehaviorBindingLogic.PutAsync(
        readinessOm.Runtime,
        new BehaviorBindingRow(
            new BehaviorBindingKey(BehaviorKind.ComputedProp, "ComputedReadyOwner", "ready_computed_prop", BehaviorCallbackSlot.Compute, "", -1),
            "binding:computedProp:ready"));
    var readyComputedCallbackCount = 0;
    readinessOm.Runtime.Registry.RegisterComputedProp(
        "ComputedReadyOwner",
        "ready_computed_prop",
        "binding:computedProp:ready",
        _ =>
        {
            readyComputedCallbackCount++;
            return ValueTask.FromResult<object?>("computedProp-ready");
        });
    Assert(AsString(await readinessOm.GetFieldValueAsync("ready:computedProp:ready:stored", "ready_computed_prop")) == "stored-ready",
        "a ready computedProp direct read should prefer the stored value");
    Assert(AsString((await readinessOm.GetObjectViewAsync("ready:computedProp:ready:stored"))!.FieldValues["ready_computed_prop"]) == "stored-ready",
        "a ready computedProp entity view should prefer the stored value");
    Assert(AsString(await readinessOm.GetFieldValueAsOfAsync(
            "ready:computedProp:ready:stored", "ready_computed_prop", "2026-01-01T00:00:00Z")) == "stored-ready",
        "a ready computedProp as-of read should prefer the stored value");
    Assert(AsString((await readinessOm.GetObjectViewAsOfAsync(
            "ready:computedProp:ready:stored", "2026-01-01T00:00:00Z"))!.FieldValues["ready_computed_prop"]) == "stored-ready",
        "a ready computedProp as-of entity view should prefer the stored value");
    Assert(readyComputedCallbackCount == 0,
        "stored values must prevent ready computedProp callbacks from running");
    Assert(AsString(await readinessOm.GetFieldValueAsync("ready:computedProp:ready:missing", "ready_computed_prop")) == "computedProp-ready",
        "a ready computedProp direct read should compute when no stored value exists");
    Assert(AsString((await readinessOm.GetObjectViewAsync("ready:computedProp:ready:missing"))!.FieldValues["ready_computed_prop"]) == "computedProp-ready",
        "a ready computedProp entity view should compute when no stored value exists");
    Assert(AsString(await readinessOm.GetFieldValueAsOfAsync(
            "ready:computedProp:ready:missing", "ready_computed_prop", "2026-01-01T00:00:00Z")) == "computedProp-ready",
        "a ready computedProp as-of read should compute when no stored value exists");
    Assert(AsString((await readinessOm.GetObjectViewAsOfAsync(
            "ready:computedProp:ready:missing", "2026-01-01T00:00:00Z"))!.FieldValues["ready_computed_prop"]) == "computedProp-ready",
        "a ready computedProp as-of entity view should compute when no stored value exists");
    Assert(readyComputedCallbackCount == 4,
        "ready computedProp callbacks should run exactly once per read only when no stored value exists");

    await readinessOm.DefineFieldAsync("ComputedUnboundOwner", "unbound_computed_prop", OmValueType.String);
    await readinessOm.DefineComputedPropAsync("ComputedUnboundOwner", "unbound_computed_prop");
    await readinessOm.CreateObjectAsync("ready:computedProp:unbound:stored", "ComputedUnboundOwner", "Unbound computedProp stored");
    await readinessOm.CreateObjectAsync("ready:computedProp:unbound:missing", "ComputedUnboundOwner", "Unbound computedProp missing");
    await readinessOm.SetFieldValueAsync(
        "ready:computedProp:unbound:stored",
        "unbound_computed_prop",
        "stored-unbound",
        new WriteOptions(ValidTime: "2025-01-01T00:00:00Z"));
    var unboundComputedCallbackCount = 0;
    readinessOm.Runtime.Registry.RegisterComputedProp(
        "ComputedUnboundOwner",
        "unbound_computed_prop",
        _ =>
        {
            unboundComputedCallbackCount++;
            return ValueTask.FromResult<object?>("computedProp-unbound");
        });
    Assert(AsString(await readinessOm.GetFieldValueAsync("ready:computedProp:unbound:stored", "unbound_computed_prop")) == "stored-unbound",
        "an unbound computedProp direct read should prefer the stored value");
    Assert(AsString((await readinessOm.GetObjectViewAsync("ready:computedProp:unbound:stored"))!.FieldValues["unbound_computed_prop"]) == "stored-unbound",
        "an unbound computedProp entity view should prefer the stored value");
    Assert(AsString(await readinessOm.GetFieldValueAsOfAsync(
            "ready:computedProp:unbound:stored", "unbound_computed_prop", "2026-01-01T00:00:00Z")) == "stored-unbound",
        "an unbound computedProp as-of read should prefer the stored value");
    Assert(AsString((await readinessOm.GetObjectViewAsOfAsync(
            "ready:computedProp:unbound:stored", "2026-01-01T00:00:00Z"))!.FieldValues["unbound_computed_prop"]) == "stored-unbound",
        "an unbound computedProp as-of entity view should prefer the stored value");
    Assert(unboundComputedCallbackCount == 0,
        "stored values must prevent unbound computedProp callbacks from running");
    Assert(AsString(await readinessOm.GetFieldValueAsync("ready:computedProp:unbound:missing", "unbound_computed_prop")) == "computedProp-unbound",
        "an unbound computedProp direct read should compute when no stored value exists");
    Assert(AsString((await readinessOm.GetObjectViewAsync("ready:computedProp:unbound:missing"))!.FieldValues["unbound_computed_prop"]) == "computedProp-unbound",
        "an unbound computedProp entity view should compute when no stored value exists");
    Assert(AsString(await readinessOm.GetFieldValueAsOfAsync(
            "ready:computedProp:unbound:missing", "unbound_computed_prop", "2026-01-01T00:00:00Z")) == "computedProp-unbound",
        "an unbound computedProp as-of read should compute when no stored value exists");
    Assert(AsString((await readinessOm.GetObjectViewAsOfAsync(
            "ready:computedProp:unbound:missing", "2026-01-01T00:00:00Z"))!.FieldValues["unbound_computed_prop"]) == "computedProp-unbound",
        "an unbound computedProp as-of entity view should compute when no stored value exists");
    Assert(unboundComputedCallbackCount == 4,
        "unbound computedProp callbacks should run exactly once per read only when no stored value exists");

    var parentFallbackRan = false;
    await readinessOm.DefineOperationAsync(
        "ReadinessBase",
        "blocked_override",
        (_, _) =>
        {
            parentFallbackRan = true;
            return ValueTask.FromResult<IReadOnlyList<MutationSpec>>([]);
        });
    await readinessOm.DefineOperationAsync("ReadinessChild", "blocked_override");
    await BehaviorBindingLogic.PutAsync(
        readinessOm.Runtime,
        new BehaviorBindingRow(
            new BehaviorBindingKey(BehaviorKind.Operation, "ReadinessChild", "blocked_override", BehaviorCallbackSlot.Handler, "", -1),
            "binding:operation:child"));
    await ExpectUnresolvedAsync(
        () => readinessOm.ExecuteOperationAsync("ready:child", "blocked_override"),
        BehaviorCatalogKind.Operation,
        "ReadinessChild",
        "operation:ReadinessChild/blocked_override",
        BehaviorCatalogCallbackSlot.Handler,
        "binding:operation:child");
    Assert(!parentFallbackRan, "an unresolved child operation definition must not fall back to a ready parent callback");

    var parentActionRan = false;
    await readinessOm.DefineOperationAsync("ReadinessBase", "blocked_parent");
    await BehaviorBindingLogic.PutAsync(
        readinessOm.Runtime,
        new BehaviorBindingRow(
            new BehaviorBindingKey(BehaviorKind.Operation, "ReadinessBase", "blocked_parent", BehaviorCallbackSlot.Handler, "", -1),
            "binding:operation:parent"));
    await readinessOm.DefineOperationAsync(
        "ReadinessChild",
        "blocked_parent",
        async (ctx, parameters) =>
        {
            await ctx.SetFieldValueAsync("effect", "parent-call-should-roll-back");
            return await ctx.CallParentOperationAsync("blocked_parent", parameters);
        });
    readinessOm.Runtime.Registry.RegisterOperation(
        "ReadinessBase",
        "blocked_parent",
        "binding:operation:parent:mismatch",
        (_, _) =>
        {
            parentActionRan = true;
            return ValueTask.FromResult<IReadOnlyList<MutationSpec>>([]);
        });
    await ExpectUnresolvedAsync(
        () => readinessOm.ExecuteOperationAsync("ready:child", "blocked_parent"),
        BehaviorCatalogKind.Operation,
        "ReadinessBase",
        "operation:ReadinessBase/blocked_parent",
        BehaviorCatalogCallbackSlot.Handler,
        "binding:operation:parent");
    Assert(!parentActionRan && AsString(await readinessOm.GetFieldValueAsync("ready:child", "effect")) == "initial",
        "an unresolved parent operation must not run its callback and must roll back prior child writes");

    var parentMutationRan = false;
    await readinessOm.DefineMutationAsync(
        "ReadinessBase",
        "blocked_mutation",
        (_, _) =>
        {
            parentMutationRan = true;
            return ValueTask.CompletedTask;
        });
    await readinessOm.DefineMutationAsync("ReadinessChild", "blocked_mutation");
    await BehaviorBindingLogic.PutAsync(
        readinessOm.Runtime,
        new BehaviorBindingRow(
            new BehaviorBindingKey(BehaviorKind.Mutation, "ReadinessChild", "blocked_mutation", BehaviorCallbackSlot.Executor, "", -1),
            "binding:mutation:child"));
    await readinessOm.DefineOperationAsync(
        "ReadinessChild",
        "returns_blocked_mutation",
        async (ctx, _) =>
        {
            await ctx.SetFieldValueAsync("effect", "mutation-should-roll-back");
            return [new MutationSpec("blocked_mutation")];
        });
    await ExpectUnresolvedAsync(
        () => readinessOm.ExecuteOperationAsync("ready:child", "returns_blocked_mutation"),
        BehaviorCatalogKind.Mutation,
        "ReadinessChild",
        "mutation:ReadinessChild/blocked_mutation",
        BehaviorCatalogCallbackSlot.Executor,
        "binding:mutation:child");
    Assert(!parentMutationRan && AsString(await readinessOm.GetFieldValueAsync("ready:child", "effect")) == "initial",
        "an unresolved nearest mutation must not fall back or leave committed operation writes");
    await ExpectUnresolvedAsync(
        () => readinessOm.ExecuteMutationsAsync("ready:child", [new MutationSpec("blocked_mutation")]),
        BehaviorCatalogKind.Mutation,
        "ReadinessChild",
        "mutation:ReadinessChild/blocked_mutation",
        BehaviorCatalogCallbackSlot.Executor,
        "binding:mutation:child");
    Assert(!parentMutationRan && AsString(await readinessOm.GetFieldValueAsync("ready:child", "effect")) == "initial",
        "direct unresolved mutation execution must remain fail closed and effect free");

    var beforeInterceptorRan = false;
    var interceptedActionRan = false;
    await readinessOm.DefineOperationAsync(
        "ReadinessChild",
        "blocked_after_interceptor",
        (_, _) =>
        {
            interceptedActionRan = true;
            return ValueTask.FromResult<IReadOnlyList<MutationSpec>>([]);
        });
    await readinessOm.AddInterceptorAsync(
        "ReadinessBase",
        "blocked_after_interceptor",
        "before",
        _ =>
        {
            beforeInterceptorRan = true;
            return ValueTask.CompletedTask;
        });
    await readinessOm.AddInterceptorAsync("ReadinessBase", "blocked_after_interceptor", "after", 7);
    await BehaviorBindingLogic.PutAsync(
        readinessOm.Runtime,
        new BehaviorBindingRow(
            new BehaviorBindingKey(BehaviorKind.Interceptor, "ReadinessBase", "blocked_after_interceptor", BehaviorCallbackSlot.Handler, "after", 7),
            "binding:interceptor:after:7"));
    await ExpectUnresolvedAsync(
        () => readinessOm.ExecuteOperationAsync("ready:child", "blocked_after_interceptor"),
        BehaviorCatalogKind.Interceptor,
        "ReadinessBase",
        "interceptor:ReadinessBase/blocked_after_interceptor/after/7",
        BehaviorCatalogCallbackSlot.Handler,
        "binding:interceptor:after:7",
        "after",
        7);
    Assert(!beforeInterceptorRan && !interceptedActionRan,
        "before and operation callbacks must not run when an inherited after interceptor is unresolved");

    var nativeActionRan = false;
    await readinessOm.DefineOperationAsync(
        "ReadinessChild",
        "native_unbound",
        (_, _) =>
        {
            nativeActionRan = true;
            return ValueTask.FromResult<IReadOnlyList<MutationSpec>>([]);
        });
    await readinessOm.ExecuteOperationAsync("ready:child", "native_unbound");
    Assert(nativeActionRan, "legacy/native behavior without a binding row must remain executable");
}

HarnessDiagnostics.Start("behavior readiness gate matrix");

using (var gateDb = new CozoDb(engine: "mem", path: ""))
{
    var gateOm = new CozoOm(gateDb);
    await gateOm.InitSchemaAsync();
    await gateOm.DefineClassAsync("GateBase", "Gate base");
    await gateOm.DefineClassAsync("GateChild", "Gate child", "GateBase");
    await gateOm.DefineFieldAsync("GateBase", "marker", OmValueType.String);
    await gateOm.CreateObjectAsync("gate:child", "GateChild", "Gate child");
    await gateOm.SetFieldValueAsync("gate:child", "marker", "stable");

    var runtimeClone = gateOm.Runtime with { Store = gateOm.Runtime.Store };
    Assert(ReferenceEquals(gateOm.Runtime.BehaviorGate, runtimeClone.BehaviorGate),
        "runtime with-expression clones must share the behavior gate");
    await using (var tx = await gateOm.Runtime.Store.BeginTransactionAsync(write: false))
    {
        var txRuntime = gateOm.Runtime with { Store = tx };
        Assert(ReferenceEquals(gateOm.Runtime.BehaviorGate, txRuntime.BehaviorGate),
            "transaction runtime clones must share the behavior gate");
    }

    await gateOm.DefineOperationAsync("GateChild", "catalog_gate");
    var catalogKey = new BehaviorBindingKey(
        BehaviorKind.Operation,
        "GateChild",
        "catalog_gate",
        BehaviorCallbackSlot.Handler,
        BehaviorBindingLogic.NonInterceptorPhase,
        BehaviorBindingLogic.NonInterceptorSeq);
    await BehaviorBindingLogic.PutAsync(gateOm.Runtime, new BehaviorBindingRow(catalogKey, "binding:catalog:pre"));
    gateOm.Runtime.Registry.RegisterOperation(
        "GateChild",
        "catalog_gate",
        "binding:catalog:pre",
        (_, _) => ValueTask.FromResult<IReadOnlyList<MutationSpec>>([]));
    var preCatalog = await gateOm.GetBehaviorCatalogAsync();
    Assert(preCatalog.Behaviors.Single(entry => entry.Name == "catalog_gate").Callbacks.Single()
            is { BindingId: "binding:catalog:pre", Readiness: BehaviorReadiness.Ready },
        "catalog should expose the complete pre-publication state");

    Task<BehaviorCatalog> blockedCatalog;
    await using (var publication = await gateOm.Runtime.BehaviorGate.EnterAsync())
    {
        blockedCatalog = gateOm.GetBehaviorCatalogAsync();
        await Task.Yield();
        Assert(!blockedCatalog.IsCompleted,
            "catalog resolution must wait while a behavior publication lease is held");
        await BehaviorBindingLogic.PutAsync(
            gateOm.Runtime,
            new BehaviorBindingRow(catalogKey, "binding:catalog:post"));
        gateOm.Runtime.Registry.RegisterOperation(
            "GateChild",
            "catalog_gate",
            "binding:catalog:post",
            (_, _) => ValueTask.FromResult<IReadOnlyList<MutationSpec>>([]));
    }

    var postCatalog = await blockedCatalog;
    Assert(postCatalog.Behaviors.Single(entry => entry.Name == "catalog_gate").Callbacks.Single()
            is { BindingId: "binding:catalog:post", Readiness: BehaviorReadiness.Ready },
        "a gated catalog reader must observe the complete post-publication state, never split binding and registry state");

    await gateOm.DefineComputedPropAsync("GateBase", "gate_computed");
    gateOm.RegisterComputedProp("GateBase", "gate_computed", _ => ValueTask.FromResult<object?>(1));
    await gateOm.DefineConstraintAsync(
        "GateBase",
        "gate_constraint",
        "conditional",
        _ => ValueTask.FromResult(true),
        _ => ValueTask.FromResult(true));
    await gateOm.DefineMutationAsync(
        "GateBase",
        "gate_mutation",
        (_, _) => ValueTask.CompletedTask);
    await gateOm.DefineOperationAsync(
        "GateChild",
        "gate_operation",
        (_, _) => ValueTask.FromResult<IReadOnlyList<MutationSpec>>([]));

    var gatedEntrypoints = new (string Name, Func<CancellationToken, Task> Start)[]
    {
        ("catalog", async token => { await gateOm.GetBehaviorCatalogAsync(token); }),
        ("constraint", async token => { await gateOm.ValidateObjectAsync("gate:child", token); }),
        ("computedProp-direct", async token => { await gateOm.GetFieldValueAsync("gate:child", "gate_computed", token); }),
        ("computedProp-view", async token => { await gateOm.GetObjectViewAsync("gate:child", token); }),
        ("computedProp-as-of", async token => { await gateOm.GetFieldValueAsOfAsync("gate:child", "gate_computed", "2026-01-01T00:00:00Z", token); }),
        ("computedProp-view-as-of", async token => { await gateOm.GetObjectViewAsOfAsync("gate:child", "2026-01-01T00:00:00Z", token); }),
        ("operation", token => gateOm.ExecuteOperationAsync("gate:child", "gate_operation", cancellationToken: token)),
        ("mutation", token => gateOm.ExecuteMutationsAsync("gate:child", [new MutationSpec("gate_mutation")], token)),
    };
    foreach (var (name, start) in gatedEntrypoints)
    {
        using var cancellation = new CancellationTokenSource();
        await using var heldGate = await gateOm.Runtime.BehaviorGate.EnterAsync();
        var waiting = start(cancellation.Token);
        while (gateOm.Runtime.BehaviorGate.WaitingCount == 0 && !waiting.IsCompleted)
        {
            await Task.Yield();
        }

        Assert(gateOm.Runtime.BehaviorGate.WaitingCount > 0 && !waiting.IsCompleted,
            $"{name} behavior resolution must wait on the shared runtime gate");
        cancellation.Cancel();
        await ExpectExceptionAsync<OperationCanceledException>(
            async () => await waiting,
            $"{name} behavior resolution must honor cancellation while waiting on the gate");
    }

    var reentered = false;
    await gateOm.DefineOperationAsync(
        "GateChild",
        "reentrant",
        async (ctx, _) =>
        {
            reentered = AsString(await ctx.GetFieldValueAsync("marker")) == "stable";
            return [];
        });
    await gateOm.ExecuteOperationAsync("gate:child", "reentrant").WaitAsync(TimeSpan.FromSeconds(2));
    Assert(reentered, "user callbacks must run after releasing the behavior gate so OM reentry cannot deadlock");

    var pipeline = new List<string>();
    await gateOm.DefineMutationAsync(
        "GateBase",
        "pipeline_mutation",
        (_, _) =>
        {
            pipeline.Add("mutation:old");
            return ValueTask.CompletedTask;
        });
    await gateOm.DefineOperationAsync(
        "GateBase",
        "pipeline_operation",
        (_, _) =>
        {
            pipeline.Add("parent:old");
            return ValueTask.FromResult<IReadOnlyList<MutationSpec>>([new MutationSpec("pipeline_mutation")]);
        });
    await gateOm.AddInterceptorAsync(
        "GateBase",
        "pipeline_operation",
        "before",
        _ =>
        {
            pipeline.Add("before:old");
            return ValueTask.CompletedTask;
        });
    await gateOm.AddInterceptorAsync(
        "GateBase",
        "pipeline_operation",
        "after",
        _ =>
        {
            pipeline.Add("after:old");
            return ValueTask.CompletedTask;
        });
    await gateOm.DefineOperationAsync(
        "GateChild",
        "pipeline_operation",
        async (ctx, parameters) =>
        {
            pipeline.Add("child");
            gateOm.Runtime.Registry.RegisterOperation(
                "GateBase",
                "pipeline_operation",
                (_, _) =>
                {
                    pipeline.Add("parent:new");
                    return ValueTask.FromResult<IReadOnlyList<MutationSpec>>([new MutationSpec("pipeline_mutation")]);
                });
            gateOm.Runtime.Registry.RegisterMutation(
                "GateBase",
                "pipeline_mutation",
                (_, _) =>
                {
                    pipeline.Add("mutation:new");
                    return ValueTask.CompletedTask;
                });
            gateOm.Runtime.Registry.RegisterInterceptor(
                "GateBase",
                "pipeline_operation",
                "after",
                0,
                _ =>
                {
                    pipeline.Add("after:new");
                    return ValueTask.CompletedTask;
                });
            return await ctx.CallParentOperationAsync("pipeline_operation", parameters);
        });

    await gateOm.ExecuteOperationAsync("gate:child", "pipeline_operation");
    Assert(pipeline.SequenceEqual(["before:old", "child", "parent:old", "mutation:old", "after:old"]),
        "operation, inherited parent, returned mutations and interceptors must all use the outer registry snapshot");
}

HarnessDiagnostics.Start("runtime registry clear lifecycle");

using (var clearDb = new CozoDb(engine: "mem", path: ""))
{
    var clearStore = new ScriptFailingOmStore(new CozoDbOmStore(clearDb));
    var clearOm = new CozoOm(clearStore);
    var peerOm = new CozoOm(new CozoDbOmStore(clearDb));
    await clearOm.InitSchemaAsync();
    await clearOm.DefineClassAsync("ClearOwner", "Registry clear owner");
    await clearOm.CreateObjectAsync("clear:1", "ClearOwner", "Clear target");
    await clearOm.DefineFieldAsync("ClearOwner", "score", OmValueType.Number);
    await clearOm.DefineConstraintAsync("ClearOwner", "custom_guard", "custom", "Clear lifecycle validator");
    await clearOm.DefineConstraintAsync("ClearOwner", "conditional_guard", "conditional", "Clear lifecycle when/then");
    await clearOm.DefineComputedPropAsync("ClearOwner", "score", "Clear lifecycle computedProp");
    await clearOm.DefineOperationAsync("ClearOwner", "work", "Clear lifecycle operation");
    await clearOm.DefineMutationAsync("ClearOwner", "work_mutation", "Clear lifecycle mutation");
    await clearOm.AddInterceptorAsync("ClearOwner", "work", "before", 3, "Clear lifecycle before");
    await clearOm.AddInterceptorAsync("ClearOwner", "work", "after", 4, "Clear lifecycle after");

    const string validatorBindingId = "clear:constraint:validator";
    const string whenBindingId = "clear:constraint:when";
    const string thenBindingId = "clear:constraint:then";
    const string computedBindingId = "clear:computedProp";
    const string actionBindingId = "clear:operation";
    const string mutationBindingId = "clear:mutation";
    const string beforeBindingId = "clear:interceptor:before:3";
    const string afterBindingId = "clear:interceptor:after:4";
    var validatorKey = new BehaviorBindingKey(
        BehaviorKind.Constraint,
        "ClearOwner",
        "custom_guard",
        BehaviorCallbackSlot.Validator,
        BehaviorBindingLogic.NonInterceptorPhase,
        BehaviorBindingLogic.NonInterceptorSeq);
    var whenKey = new BehaviorBindingKey(
        BehaviorKind.Constraint,
        "ClearOwner",
        "conditional_guard",
        BehaviorCallbackSlot.When,
        BehaviorBindingLogic.NonInterceptorPhase,
        BehaviorBindingLogic.NonInterceptorSeq);
    var thenKey = new BehaviorBindingKey(
        BehaviorKind.Constraint,
        "ClearOwner",
        "conditional_guard",
        BehaviorCallbackSlot.Then,
        BehaviorBindingLogic.NonInterceptorPhase,
        BehaviorBindingLogic.NonInterceptorSeq);
    var computedKey = new BehaviorBindingKey(
        BehaviorKind.ComputedProp,
        "ClearOwner",
        "score",
        BehaviorCallbackSlot.Compute,
        BehaviorBindingLogic.NonInterceptorPhase,
        BehaviorBindingLogic.NonInterceptorSeq);
    var actionKey = new BehaviorBindingKey(
        BehaviorKind.Operation,
        "ClearOwner",
        "work",
        BehaviorCallbackSlot.Handler,
        BehaviorBindingLogic.NonInterceptorPhase,
        BehaviorBindingLogic.NonInterceptorSeq);
    var mutationKey = new BehaviorBindingKey(
        BehaviorKind.Mutation,
        "ClearOwner",
        "work_mutation",
        BehaviorCallbackSlot.Executor,
        BehaviorBindingLogic.NonInterceptorPhase,
        BehaviorBindingLogic.NonInterceptorSeq);
    var beforeKey = new BehaviorBindingKey(
        BehaviorKind.Interceptor,
        "ClearOwner",
        "work",
        BehaviorCallbackSlot.Handler,
        "before",
        3);
    var afterKey = new BehaviorBindingKey(
        BehaviorKind.Interceptor,
        "ClearOwner",
        "work",
        BehaviorCallbackSlot.Handler,
        "after",
        4);
    await BehaviorBindingLogic.PutAsync(clearOm.Runtime, new BehaviorBindingRow(validatorKey, validatorBindingId));
    await BehaviorBindingLogic.PutAsync(clearOm.Runtime, new BehaviorBindingRow(whenKey, whenBindingId));
    await BehaviorBindingLogic.PutAsync(clearOm.Runtime, new BehaviorBindingRow(thenKey, thenBindingId));
    await BehaviorBindingLogic.PutAsync(clearOm.Runtime, new BehaviorBindingRow(computedKey, computedBindingId));
    await BehaviorBindingLogic.PutAsync(clearOm.Runtime, new BehaviorBindingRow(actionKey, actionBindingId));
    await BehaviorBindingLogic.PutAsync(clearOm.Runtime, new BehaviorBindingRow(mutationKey, mutationBindingId));
    await BehaviorBindingLogic.PutAsync(clearOm.Runtime, new BehaviorBindingRow(beforeKey, beforeBindingId));
    await BehaviorBindingLogic.PutAsync(clearOm.Runtime, new BehaviorBindingRow(afterKey, afterBindingId));

    clearOm.Runtime.Registry.RegisterValidator(
        "ClearOwner",
        "custom_guard",
        validatorBindingId,
        _ => ValueTask.FromResult<string?>(null));
    clearOm.Runtime.Registry.RegisterConstraint(
        "ClearOwner",
        "conditional_guard",
        whenBindingId,
        _ => ValueTask.FromResult(true),
        thenBindingId,
        _ => ValueTask.FromResult(true));
    clearOm.Runtime.Registry.RegisterComputedProp(
        "ClearOwner",
        "score",
        computedBindingId,
        _ => ValueTask.FromResult<object?>(42));
    var pipeline = new List<string>();
    var actionStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    var releaseOperation = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    clearOm.Runtime.Registry.RegisterOperation(
        "ClearOwner",
        "work",
        actionBindingId,
        async (_, _) =>
        {
            pipeline.Add("operation");
            actionStarted.SetResult();
            await releaseOperation.Task;
            return [new MutationSpec("work_mutation")];
        });
    clearOm.Runtime.Registry.RegisterMutation(
        "ClearOwner",
        "work_mutation",
        mutationBindingId,
        (_, _) =>
        {
            pipeline.Add("mutation");
            return ValueTask.CompletedTask;
        });
    clearOm.Runtime.Registry.RegisterInterceptor(
        "ClearOwner",
        "work",
        "before",
        3,
        beforeBindingId,
        _ =>
        {
            pipeline.Add("before");
            return ValueTask.CompletedTask;
        });
    clearOm.Runtime.Registry.RegisterInterceptor(
        "ClearOwner",
        "work",
        "after",
        4,
        afterBindingId,
        _ =>
        {
            pipeline.Add("after");
            return ValueTask.CompletedTask;
        });

    var peerRuns = 0;
    peerOm.Runtime.Registry.RegisterValidator(
        "ClearOwner",
        "custom_guard",
        validatorBindingId,
        _ => ValueTask.FromResult<string?>(null));
    peerOm.Runtime.Registry.RegisterConstraint(
        "ClearOwner",
        "conditional_guard",
        whenBindingId,
        _ => ValueTask.FromResult(true),
        thenBindingId,
        _ => ValueTask.FromResult(true));
    peerOm.Runtime.Registry.RegisterComputedProp(
        "ClearOwner",
        "score",
        computedBindingId,
        _ => ValueTask.FromResult<object?>(84));
    peerOm.Runtime.Registry.RegisterOperation(
        "ClearOwner",
        "work",
        actionBindingId,
        (_, _) =>
        {
            peerRuns++;
            return ValueTask.FromResult<IReadOnlyList<MutationSpec>>([]);
        });
    peerOm.Runtime.Registry.RegisterMutation(
        "ClearOwner",
        "work_mutation",
        mutationBindingId,
        (_, _) => ValueTask.CompletedTask);
    peerOm.Runtime.Registry.RegisterInterceptor(
        "ClearOwner",
        "work",
        "before",
        3,
        beforeBindingId,
        _ => ValueTask.CompletedTask);
    peerOm.Runtime.Registry.RegisterInterceptor(
        "ClearOwner",
        "work",
        "after",
        4,
        afterBindingId,
        _ => ValueTask.CompletedTask);

    var catalogBeforeClear = await clearOm.GetBehaviorCatalogAsync();
    var metadataBeforeClear = CaptureBehaviorMetadata(catalogBeforeClear);
    var bindingsBeforeClear = await BehaviorBindingLogic.ListAsync(clearOm.Runtime);
    var expectedLifecycleBindings = new HashSet<(string BindingId, BehaviorCatalogCallbackSlot Slot)>
    {
        (validatorBindingId, BehaviorCatalogCallbackSlot.Validator),
        (whenBindingId, BehaviorCatalogCallbackSlot.When),
        (thenBindingId, BehaviorCatalogCallbackSlot.Then),
        (computedBindingId, BehaviorCatalogCallbackSlot.Compute),
        (actionBindingId, BehaviorCatalogCallbackSlot.Handler),
        (mutationBindingId, BehaviorCatalogCallbackSlot.Executor),
        (beforeBindingId, BehaviorCatalogCallbackSlot.Handler),
        (afterBindingId, BehaviorCatalogCallbackSlot.Handler),
    };
    var callbacksBeforeClear = catalogBeforeClear.Behaviors
        .SelectMany(entry => entry.Callbacks)
        .Where(callback => callback.BindingId is not null)
        .ToArray();
    Assert(callbacksBeforeClear.Select(callback => (callback.BindingId!, callback.Slot))
               .ToHashSet().SetEquals(expectedLifecycleBindings)
           && callbacksBeforeClear.All(callback => callback.Readiness == BehaviorReadiness.Ready),
        "validator, when/then constraint, computedProp, operation, mutation and interceptor slots must all be ready before registry clear");

    var inFlight = clearOm.ExecuteOperationAsync("clear:1", "work");
    await actionStarted.Task.WaitAsync(TimeSpan.FromSeconds(2));
    Assert(pipeline.SequenceEqual(["before", "operation"]),
        "the in-flight operation must capture and begin the old pipeline before clear");

    clearStore.FailWhenScriptContains = "";
    clearStore.FailBeginTransaction = true;
    await clearOm.ClearRegistryAsync();
    clearStore.FailWhenScriptContains = null;
    clearStore.FailBeginTransaction = false;
    Assert(ReferenceEquals(clearOm.Runtime.Registry.CaptureSnapshot(), CozoOmRegistrySnapshot.Empty),
        "clear must atomically publish the canonical empty registry snapshot without querying CozoDB");

    releaseOperation.SetResult();
    await inFlight.WaitAsync(TimeSpan.FromSeconds(2));
    Assert(pipeline.SequenceEqual(["before", "operation", "mutation", "after"]),
        "an in-flight operation, mutation and interceptor pipeline must finish on its captured pre-clear snapshot");

    var catalogAfterClear = await clearOm.GetBehaviorCatalogAsync();
    var bindingsAfterClear = await BehaviorBindingLogic.ListAsync(clearOm.Runtime);
    Assert(metadataBeforeClear.SequenceEqual(CaptureBehaviorMetadata(catalogAfterClear))
           && bindingsBeforeClear.SequenceEqual(bindingsAfterClear),
        "registry clear must preserve behavior definitions and binding identities");
    var callbacksAfterClear = catalogAfterClear.Behaviors
        .SelectMany(entry => entry.Callbacks)
        .Where(callback => callback.BindingId is not null)
        .ToArray();
    Assert(callbacksAfterClear.Select(callback => (callback.BindingId!, callback.Slot))
               .ToHashSet().SetEquals(expectedLifecycleBindings)
           && callbacksAfterClear.All(callback => callback.Readiness == BehaviorReadiness.Unresolved),
        "the same clear must make validator, when/then constraint, computedProp, operation, mutation and interceptor slots unresolved");
    await ExpectUnresolvedAsync(
        () => clearOm.ExecuteOperationAsync("clear:1", "work"),
        BehaviorCatalogKind.Operation,
        "ClearOwner",
        "operation:ClearOwner/work",
        BehaviorCatalogCallbackSlot.Handler,
        actionBindingId);

    await peerOm.ExecuteOperationAsync("clear:1", "work");
    Assert(peerRuns == 1
           && (await peerOm.GetBehaviorCatalogAsync()).Behaviors.SelectMany(entry => entry.Callbacks)
               .All(callback => callback.Readiness == BehaviorReadiness.Ready),
        "clearing one CozoOm registry must not affect another instance sharing the same persistent store");

    await clearOm.ClearRegistryAsync();
    Assert(ReferenceEquals(clearOm.Runtime.Registry.CaptureSnapshot(), CozoOmRegistrySnapshot.Empty),
        "clearing an already empty registry must be idempotent");

    clearOm.Runtime.Registry.RegisterOperation(
        "ClearOwner",
        "work",
        actionBindingId,
        (_, _) => ValueTask.FromResult<IReadOnlyList<MutationSpec>>([]));
    var registryBeforeCancelledClear = clearOm.Runtime.Registry.CaptureSnapshot();
    using (var cancellation = new CancellationTokenSource())
    {
        await using var heldGate = await clearOm.Runtime.BehaviorGate.EnterAsync();
        var cancelledClear = clearOm.ClearRegistryAsync(cancellation.Token);
        while (clearOm.Runtime.BehaviorGate.WaitingCount == 0 && !cancelledClear.IsCompleted)
        {
            await Task.Yield();
        }

        Assert(clearOm.Runtime.BehaviorGate.WaitingCount > 0 && !cancelledClear.IsCompleted,
            "registry clear must wait behind an in-flight behavior publication");
        cancellation.Cancel();
        await ExpectExceptionAsync<OperationCanceledException>(
            async () => await cancelledClear,
            "registry clear must honor cancellation while waiting on the behavior gate");
    }

    Assert(ReferenceEquals(clearOm.Runtime.Registry.CaptureSnapshot(), registryBeforeCancelledClear),
        "a cancelled registry clear must not publish an empty snapshot");

    Task gatedClear;
    await using (var heldGate = await clearOm.Runtime.BehaviorGate.EnterAsync())
    {
        gatedClear = clearOm.ClearRegistryAsync();
        while (clearOm.Runtime.BehaviorGate.WaitingCount == 0 && !gatedClear.IsCompleted)
        {
            await Task.Yield();
        }

        Assert(clearOm.Runtime.BehaviorGate.WaitingCount > 0 && !gatedClear.IsCompleted,
            "registry clear must serialize behind an in-flight import/publication gate lease");
        Assert(ReferenceEquals(clearOm.Runtime.Registry.CaptureSnapshot(), registryBeforeCancelledClear),
            "registry clear must not publish before it acquires the behavior gate");
    }

    await gatedClear;
    Assert(ReferenceEquals(clearOm.Runtime.Registry.CaptureSnapshot(), CozoOmRegistrySnapshot.Empty),
        "registry clear must publish one complete empty snapshot after the behavior gate is released");
}

HarnessDiagnostics.Start("behavior import and rollback matrix");

using (var importDb = new CozoDb(engine: "mem", path: ""))
{
    var importOm = new CozoOm(importDb);
    await importOm.InitSchemaAsync();
    await importOm.DefineClassAsync("ImportOwner", "Behavior import owner");
    await importOm.CreateObjectAsync("import:1", "ImportOwner", "Imported entity");

    var importCatalog = new BehaviorCatalog([
        new BehaviorCatalogEntry(
            BehaviorCatalogKind.Constraint, "ImportOwner", "portable_conditional_constraint", "conditional", "portable conditional constraint", null, null, null,
            [
                new(BehaviorCatalogCallbackSlot.When, "import:constraint:when", BehaviorReadiness.Ready),
                new(BehaviorCatalogCallbackSlot.Then, "import:constraint:then", BehaviorReadiness.Ready),
            ]),
        new BehaviorCatalogEntry(
            BehaviorCatalogKind.Constraint, "ImportOwner", "portable_custom_constraint", "custom", "portable custom constraint", null, null, null,
            [
                new(BehaviorCatalogCallbackSlot.Validator, "import:constraint:validator", BehaviorReadiness.Ready),
            ]),
        new BehaviorCatalogEntry(
            BehaviorCatalogKind.ComputedProp, "ImportOwner", "portable_computed_prop", null, null, "portable computedProp", null, null,
            [new(BehaviorCatalogCallbackSlot.Compute, "import:computedProp", BehaviorReadiness.Ready)]),
        new BehaviorCatalogEntry(
            BehaviorCatalogKind.Operation, "ImportOwner", "portable_operation", null, null, "portable operation", null, null,
            [new(BehaviorCatalogCallbackSlot.Handler, "import:operation", BehaviorReadiness.Ready)]),
        new BehaviorCatalogEntry(
            BehaviorCatalogKind.Mutation, "ImportOwner", "portable_mutation", null, null, "portable mutation", null, null,
            [new(BehaviorCatalogCallbackSlot.Executor, "import:mutation", BehaviorReadiness.Ready)]),
        new BehaviorCatalogEntry(
            BehaviorCatalogKind.Interceptor, "ImportOwner", "portable_operation", null, null, "portable before", "before", 7,
            [new(BehaviorCatalogCallbackSlot.Handler, "import:interceptor:7", BehaviorReadiness.Ready)]),
    ]);
    var importJson = Encoding.UTF8.GetString(BehaviorManifestJsonCodec.Encode(importCatalog));
    var mutableActions = new List<BehaviorOperationCallbackBinding>
    {
        new("import:operation", (_, _) => ValueTask.FromResult<IReadOnlyList<MutationSpec>>([])),
    };
    var importConstraintWhenRan = false;
    var mixedCallbacks = new BehaviorCallbackBindingSet(
        constraints: [new("import:constraint:when", _ => { importConstraintWhenRan = true; return ValueTask.FromResult(true); })],
        validators: [new("import:constraint:validator", _ => ValueTask.FromResult<string?>(null))],
        computedProp: [new("import:computedProp", _ => ValueTask.FromResult<object?>(42))],
        operations: mutableActions,
        mutations: [new("import:mutation", (_, _) => ValueTask.CompletedTask)],
        interceptors: [new("import:interceptor:7", _ => ValueTask.CompletedTask)]);
    mutableActions.Clear();

    var permissive = await importOm.ImportBehaviorManifestJsonAsync(importJson, mixedCallbacks);
    Assert(permissive.Applied && permissive.Unresolved.Length == 1
           && permissive.Unresolved[0] is { Slot: BehaviorCatalogCallbackSlot.Then, BindingId: "import:constraint:then" }
           && permissive.Diagnostics.Any(diagnostic => diagnostic.Code == "OMI1201"),
        "default import should apply all metadata and return exact unresolved diagnostics for absent typed callbacks");
    var imported = await importOm.GetBehaviorCatalogAsync();
    Assert(imported.Behaviors.Count(entry => entry.OwnerClass == "ImportOwner") == 6
           && imported.Behaviors.Where(entry => entry.OwnerClass == "ImportOwner").Select(entry => entry.Kind).Distinct().Count() == 5,
        "default import should persist both legal constraint shapes and all five behavior kinds");
    Assert(imported.Behaviors.SelectMany(entry => entry.Callbacks)
            .Single(callback => callback.BindingId == "import:constraint:then").Readiness == BehaviorReadiness.Unresolved
           && imported.Behaviors.SelectMany(entry => entry.Callbacks)
               .Where(callback => callback.BindingId != "import:constraint:then")
               .All(callback => callback.Readiness == BehaviorReadiness.Ready),
        "manifest ready projection must be ignored; only exact supplied typed callbacks become ready");
    await ExpectExceptionAsync<BehaviorUnresolvedException>(
        () => importOm.ValidateObjectAsync("import:1"),
        "a permissively imported unresolved constraint must fail closed at the existing execution gate");
    Assert(!importConstraintWhenRan,
        "all imported constraint slots must be pre-resolved before any user callback runs");
    await importOm.ExecuteOperationAsync("import:1", "portable_operation");

    var importedConditionalWhenRan = false;
    var importedConditionalThenRan = false;
    var importedCustomValidatorRan = false;
    var fullCallbacks = new BehaviorCallbackBindingSet(
        constraints:
        [
            new("import:constraint:when", _ => { importedConditionalWhenRan = true; return ValueTask.FromResult(true); }),
            new("import:constraint:then", _ => { importedConditionalThenRan = true; return ValueTask.FromResult(true); }),
        ],
        validators: [new("import:constraint:validator", _ => { importedCustomValidatorRan = true; return ValueTask.FromResult<string?>(null); })],
        computedProp: [new("import:computedProp", _ => ValueTask.FromResult<object?>(43))],
        operations: [new("import:operation", (_, _) => ValueTask.FromResult<IReadOnlyList<MutationSpec>>([]))],
        mutations: [new("import:mutation", (_, _) => ValueTask.CompletedTask)],
        interceptors: [new("import:interceptor:7", _ => ValueTask.CompletedTask)]);

    var invalidConstraintShapes = new[]
    {
        new
        {
            Name = "invalid_conditional_missing_then",
            ConstraintKind = "conditional",
            Callbacks = new[]
            {
                new BehaviorCallbackBinding(BehaviorCatalogCallbackSlot.When, "import:constraint:when", BehaviorReadiness.Ready),
            },
            Missing = "then",
            Extra = "none",
        },
        new
        {
            Name = "invalid_cross_entity_validator_only",
            ConstraintKind = "cross-entity",
            Callbacks = new[]
            {
                new BehaviorCallbackBinding(BehaviorCatalogCallbackSlot.Validator, "import:constraint:validator", BehaviorReadiness.Ready),
            },
            Missing = "when, then",
            Extra = "validator",
        },
        new
        {
            Name = "invalid_computed_dep_extra_validator",
            ConstraintKind = "computedProp-dep",
            Callbacks = new[]
            {
                new BehaviorCallbackBinding(BehaviorCatalogCallbackSlot.When, "import:constraint:when", BehaviorReadiness.Ready),
                new BehaviorCallbackBinding(BehaviorCatalogCallbackSlot.Then, "import:constraint:then", BehaviorReadiness.Ready),
                new BehaviorCallbackBinding(BehaviorCatalogCallbackSlot.Validator, "import:constraint:validator", BehaviorReadiness.Ready),
            },
            Missing = "none",
            Extra = "validator",
        },
        new
        {
            Name = "invalid_custom_missing_validator",
            ConstraintKind = "custom",
            Callbacks = Array.Empty<BehaviorCallbackBinding>(),
            Missing = "validator",
            Extra = "none",
        },
        new
        {
            Name = "invalid_custom_extra_when_then",
            ConstraintKind = "custom",
            Callbacks = new[]
            {
                new BehaviorCallbackBinding(BehaviorCatalogCallbackSlot.When, "import:constraint:when", BehaviorReadiness.Ready),
                new BehaviorCallbackBinding(BehaviorCatalogCallbackSlot.Then, "import:constraint:then", BehaviorReadiness.Ready),
                new BehaviorCallbackBinding(BehaviorCatalogCallbackSlot.Validator, "import:constraint:validator", BehaviorReadiness.Ready),
            },
            Missing = "none",
            Extra = "when, then",
        },
    };
    foreach (var invalidShape in invalidConstraintShapes)
    {
        var invalidCatalog = new BehaviorCatalog([
            new BehaviorCatalogEntry(
                BehaviorCatalogKind.Constraint,
                "ImportOwner",
                invalidShape.Name,
                invalidShape.ConstraintKind,
                "invalid constraint shape",
                null,
                null,
                null,
                invalidShape.Callbacks),
        ]);
        var invalidJson = Encoding.UTF8.GetString(BehaviorManifestJsonCodec.Encode(invalidCatalog));
        var shapeBefore = BehaviorManifestJsonCodec.Encode(await importOm.GetBehaviorCatalogAsync());
        var shapeRegistryBefore = importOm.Runtime.Registry.CaptureSnapshot();
        var defaultShapeFailure = await importOm.ImportBehaviorManifestJsonAsync(invalidJson, fullCallbacks);
        var strictShapeFailure = await importOm.ImportBehaviorManifestJsonAsync(
            invalidJson,
            fullCallbacks,
            new BehaviorImportOptions(RequireReady: true));
        var shapeAfter = BehaviorManifestJsonCodec.Encode(await importOm.GetBehaviorCatalogAsync());
        var defaultDiagnostic = defaultShapeFailure.Diagnostics.Single(diagnostic => diagnostic.Code == "OMI1104");
        var strictDiagnostic = strictShapeFailure.Diagnostics.Single(diagnostic => diagnostic.Code == "OMI1104");
        Assert(!defaultShapeFailure.Applied && !strictShapeFailure.Applied
               && defaultDiagnostic == strictDiagnostic
               && defaultDiagnostic is
               {
                   Kind: BehaviorCatalogKind.Constraint,
                   OwnerClass: "ImportOwner",
                   BehaviorName: var diagnosticName,
                   Slot: null,
               }
               && diagnosticName == invalidShape.Name
               && defaultDiagnostic.Path == $"constraint:ImportOwner/{invalidShape.Name}.callbacks"
               && defaultDiagnostic.Message.Contains($"missing slots [{invalidShape.Missing}]", StringComparison.Ordinal)
               && defaultDiagnostic.Message.Contains($"extra slots [{invalidShape.Extra}]", StringComparison.Ordinal)
               && shapeBefore.SequenceEqual(shapeAfter)
               && ReferenceEquals(shapeRegistryBefore, importOm.Runtime.Registry.CaptureSnapshot()),
            $"constraint shape '{invalidShape.Name}' must fail stable preflight in both modes without metadata, binding or registry effects");
    }

    var beforeStrict = BehaviorManifestJsonCodec.Encode(await importOm.GetBehaviorCatalogAsync());
    var beforeStrictSnapshot = importOm.Runtime.Registry.CaptureSnapshot();
    var strictRejected = await importOm.ImportBehaviorManifestJsonAsync(
        importJson,
        new BehaviorCallbackBindingSet(),
        new BehaviorImportOptions(RequireReady: true));
    var afterStrict = BehaviorManifestJsonCodec.Encode(await importOm.GetBehaviorCatalogAsync());
    Assert(!strictRejected.Applied && strictRejected.Unresolved.Length == 7
           && beforeStrict.SequenceEqual(afterStrict)
           && ReferenceEquals(beforeStrictSnapshot, importOm.Runtime.Registry.CaptureSnapshot()),
        "requireReady must reject missing typed callbacks before metadata, binding, registry or readiness effects");

    var facadePreflightState = await CaptureBehaviorStateAsync(importOm);
    var malformed = await importOm.ImportBehaviorManifestJsonAsync("{", fullCallbacks);
    var malformedAfter = await CaptureBehaviorStateAsync(importOm);
    Assert(!malformed.Applied && malformed.Diagnostics.Any(diagnostic => diagnostic.Code == "OMM1000")
           && malformed.Unresolved.IsEmpty,
        "malformed manifests must fail pure decode without effects");
    AssertBehaviorStateUnchanged(
        facadePreflightState,
        malformedAfter,
        "malformed manifests must leave metadata, binding rows, registry and readiness unchanged");

    var facadePreflightFailures = new[]
    {
        new
        {
            Name = "unknown-version",
            Json = """{"version":99,"behaviors":[]}""",
            Expected = new (string Code, string Path, string Message)[]
            {
                ("OMM1002", "$.version", "Manifest version '99' is not supported."),
            },
        },
        new
        {
            Name = "unknown-kind",
            Json = """
                {"version":1,"behaviors":[{"kind":"mystery","ownerClass":"ImportOwner","name":"unknown_kind","constraintKind":null,"message":null,"description":null,"interceptorPhase":null,"interceptorSeq":null,"callbacks":[]}]}
                """,
            Expected = new (string Code, string Path, string Message)[]
            {
                ("OMM1101", "$.behaviors[0].kind", "Behavior kind 'mystery' is unknown."),
            },
        },
        new
        {
            Name = "invalid-metadata-matrix",
            Json = """
                {
                  "version": 1,
                  "behaviors": [
                    { "kind": "constraint", "ownerClass": "ImportOwner", "name": "invalid_constraint_metadata", "constraintKind": null, "message": "allowed", "description": "forbidden", "interceptorPhase": "before", "interceptorSeq": 0, "callbacks": [] },
                    { "kind": "computedProp", "ownerClass": "ImportOwner", "name": "invalid_computed_metadata", "constraintKind": "custom", "message": "forbidden", "description": "allowed", "interceptorPhase": null, "interceptorSeq": null, "callbacks": [] },
                    { "kind": "operation", "ownerClass": "ImportOwner", "name": "invalid_operation_metadata", "constraintKind": "custom", "message": "forbidden", "description": "allowed", "interceptorPhase": null, "interceptorSeq": null, "callbacks": [] },
                    { "kind": "mutation", "ownerClass": "ImportOwner", "name": "invalid_mutation_metadata", "constraintKind": "custom", "message": "forbidden", "description": "allowed", "interceptorPhase": "after", "interceptorSeq": 2, "callbacks": [] },
                    { "kind": "interceptor", "ownerClass": "ImportOwner", "name": "invalid_interceptor_metadata", "constraintKind": "custom", "message": "forbidden", "description": "allowed", "interceptorPhase": "after", "interceptorSeq": 3, "callbacks": [] }
                  ]
                }
                """,
            Expected = new (string Code, string Path, string Message)[]
            {
                ("OMM1203", "$.behaviors[0].constraintKind", "Property 'constraintKind' must be a string for behavior kind 'constraint'."),
                ("OMM1203", "$.behaviors[0].description", "Property 'description' must be null for behavior kind 'constraint'."),
                ("OMM1203", "$.behaviors[0].interceptorPhase", "Property 'interceptorPhase' must be null for behavior kind 'constraint'."),
                ("OMM1203", "$.behaviors[0].interceptorSeq", "Property 'interceptorSeq' must be null for behavior kind 'constraint'."),
                ("OMM1203", "$.behaviors[1].constraintKind", "Property 'constraintKind' must be null for behavior kind 'computedProp'."),
                ("OMM1203", "$.behaviors[1].message", "Property 'message' must be null for behavior kind 'computedProp'."),
                ("OMM1203", "$.behaviors[2].constraintKind", "Property 'constraintKind' must be null for behavior kind 'operation'."),
                ("OMM1203", "$.behaviors[2].message", "Property 'message' must be null for behavior kind 'operation'."),
                ("OMM1203", "$.behaviors[3].constraintKind", "Property 'constraintKind' must be null for behavior kind 'mutation'."),
                ("OMM1203", "$.behaviors[3].interceptorPhase", "Property 'interceptorPhase' must be null for behavior kind 'mutation'."),
                ("OMM1203", "$.behaviors[3].interceptorSeq", "Property 'interceptorSeq' must be null for behavior kind 'mutation'."),
                ("OMM1203", "$.behaviors[3].message", "Property 'message' must be null for behavior kind 'mutation'."),
                ("OMM1203", "$.behaviors[4].constraintKind", "Property 'constraintKind' must be null for behavior kind 'interceptor'."),
                ("OMM1203", "$.behaviors[4].message", "Property 'message' must be null for behavior kind 'interceptor'."),
            },
        },
        new
        {
            Name = "invalid-behavior-key",
            Json = """
                {"version":1,"behaviors":[{"kind":"operation","ownerClass":"ImportOwner","name":"invalid_key","constraintKind":null,"message":null,"description":null,"interceptorPhase":"before","interceptorSeq":0,"callbacks":[{"slot":"handler","bindingId":"invalid:key","readiness":"ready"}]}]}
                """,
            Expected = new (string Code, string Path, string Message)[]
            {
                ("OMM1203", "$.behaviors[0].interceptorPhase", "Property 'interceptorPhase' must be null for behavior kind 'operation'."),
                ("OMM1203", "$.behaviors[0].interceptorSeq", "Property 'interceptorSeq' must be null for behavior kind 'operation'."),
            },
        },
        new
        {
            Name = "invalid-interceptor-key",
            Json = """
                {"version":1,"behaviors":[{"kind":"interceptor","ownerClass":"ImportOwner","name":"invalid_interceptor_key","constraintKind":null,"message":null,"description":"allowed","interceptorPhase":"during","interceptorSeq":-1,"callbacks":[]}]}
                """,
            Expected = new (string Code, string Path, string Message)[]
            {
                ("OMM1201", "$.behaviors[0]", "Interceptor keys require phase before/after and a non-negative sequence."),
            },
        },
        new
        {
            Name = "invalid-slot-key",
            Json = """
                {"version":1,"behaviors":[{"kind":"operation","ownerClass":"ImportOwner","name":"invalid_slot","constraintKind":null,"message":null,"description":null,"interceptorPhase":null,"interceptorSeq":null,"callbacks":[{"slot":"compute","bindingId":"invalid:slot","readiness":"ready"}]}]}
                """,
            Expected = new (string Code, string Path, string Message)[]
            {
                ("OMM1202", "$.behaviors[0].callbacks[0]", "Callback slot 'compute' is invalid for behavior kind 'operation'."),
            },
        },
        new
        {
            Name = "duplicate-exact-interceptor-key",
            Json = """
                {"version":1,"behaviors":[{"kind":"interceptor","ownerClass":"ImportOwner","name":"portable_operation","constraintKind":null,"message":null,"description":"duplicate","interceptorPhase":"before","interceptorSeq":7,"callbacks":[{"slot":"handler","bindingId":"duplicate:handler","readiness":"ready"}]},{"kind":"interceptor","ownerClass":"ImportOwner","name":"portable_operation","constraintKind":null,"message":null,"description":"duplicate","interceptorPhase":"before","interceptorSeq":7,"callbacks":[{"slot":"handler","bindingId":"duplicate:handler","readiness":"ready"}]}]}
                """,
            Expected = new (string Code, string Path, string Message)[]
            {
                ("OMM1301", "$.behaviors[1].callbacks[0]", "Callback binding key 'interceptor:ImportOwner/portable_operation/before/7/handler' is duplicated or conflicting."),
                ("OMM1302", "$.behaviors[1]", "Behavior key 'interceptor:ImportOwner/portable_operation/before/7' is duplicated or conflicting."),
            },
        },
        new
        {
            Name = "conflicting-exact-interceptor-key",
            Json = """
                {"version":1,"behaviors":[{"kind":"interceptor","ownerClass":"ImportOwner","name":"portable_operation","constraintKind":null,"message":null,"description":"first","interceptorPhase":"before","interceptorSeq":7,"callbacks":[{"slot":"handler","bindingId":"conflict:first","readiness":"ready"}]},{"kind":"interceptor","ownerClass":"ImportOwner","name":"portable_operation","constraintKind":null,"message":null,"description":"second","interceptorPhase":"before","interceptorSeq":7,"callbacks":[{"slot":"handler","bindingId":"conflict:second","readiness":"ready"}]}]}
                """,
            Expected = new (string Code, string Path, string Message)[]
            {
                ("OMM1301", "$.behaviors[1].callbacks[0]", "Callback binding key 'interceptor:ImportOwner/portable_operation/before/7/handler' is duplicated or conflicting."),
                ("OMM1302", "$.behaviors[1]", "Behavior key 'interceptor:ImportOwner/portable_operation/before/7' is duplicated or conflicting."),
            },
        },
    };
    foreach (var failureCase in facadePreflightFailures)
    {
        var result = await importOm.ImportBehaviorManifestJsonAsync(failureCase.Json, fullCallbacks);
        var actualDiagnostics = result.Diagnostics
            .Select(diagnostic => (diagnostic.Code, diagnostic.Path, diagnostic.Message))
            .ToArray();
        Assert(!result.Applied
               && result.Unresolved.IsEmpty
               && actualDiagnostics.SequenceEqual(failureCase.Expected)
               && result.Diagnostics.All(diagnostic => diagnostic is
               {
                   Kind: null,
                   OwnerClass: null,
                   BehaviorName: null,
                   Slot: null,
                   BindingId: null,
                   InterceptorPhase: null,
                   InterceptorSeq: null,
               }),
            $"{failureCase.Name} must fail through the import facade with exact structured decode diagnostics");
        AssertBehaviorStateUnchanged(
            facadePreflightState,
            await CaptureBehaviorStateAsync(importOm),
            $"{failureCase.Name} must be pure preflight with zero metadata, binding, registry or readiness effects");
    }

    using (var importCancellation = new CancellationTokenSource())
    {
        await using var heldImportGate = await importOm.Runtime.BehaviorGate.EnterAsync();
        var cancelledImport = importOm.ImportBehaviorManifestJsonAsync(importJson, fullCallbacks, cancellationToken: importCancellation.Token);
        while (importOm.Runtime.BehaviorGate.WaitingCount == 0 && !cancelledImport.IsCompleted)
        {
            await Task.Yield();
        }
        importCancellation.Cancel();
        await ExpectExceptionAsync<OperationCanceledException>(
            async () => await cancelledImport,
            "manifest import must honor cancellation while waiting for the runtime gate");
    }

    var changedCatalog = new BehaviorCatalog(importCatalog.Behaviors.Select(entry => entry with
    {
        Description = entry.Description is null ? null : entry.Description + " changed",
        Message = entry.Message is null ? null : entry.Message + " changed",
    }));
    var changedJson = Encoding.UTF8.GetString(BehaviorManifestJsonCodec.Encode(changedCatalog));

    var faultBaseline = await CaptureBehaviorStateAsync(importOm);
    importOm.Runtime.BehaviorImportFaults.BeforePersistentCommitAsync = _ =>
        Task.FromException(new InvalidOperationException("simulated import persistence failure"));
    var persistentException = await CaptureExceptionAsync<BehaviorImportException>(
        () => importOm.ImportBehaviorManifestJsonAsync(changedJson, fullCallbacks),
        "persistent import failure should surface as a structured import exception");
    importOm.Runtime.BehaviorImportFaults.Reset();
    Assert(persistentException.Message == "Behavior manifest persistence failed before registry publication."
           && persistentException.OriginalFailure is InvalidOperationException
           {
               Message: "simulated import persistence failure",
           }
           && ReferenceEquals(persistentException.InnerException, persistentException.OriginalFailure)
           && persistentException.CompensationFailures.IsEmpty,
        "persistent failure must preserve the exact structured exception chain without compensation claims");
    AssertBehaviorStateUnchanged(
        faultBaseline,
        await CaptureBehaviorStateAsync(importOm),
        "persistent import failure must leave metadata, binding rows, registry and readiness unchanged");

    importOm.Runtime.BehaviorImportFaults.BeforeRegistryPublish = () =>
        throw new InvalidOperationException("simulated registry publication failure");
    var publishException = await CaptureExceptionAsync<BehaviorImportException>(
        () => importOm.ImportBehaviorManifestJsonAsync(changedJson, fullCallbacks),
        "registry publication failure should surface as a structured import exception");
    importOm.Runtime.BehaviorImportFaults.Reset();
    Assert(publishException.Message == "Behavior registry publication failed; persistent state was restored."
           && publishException.OriginalFailure is InvalidOperationException
           {
               Message: "simulated registry publication failure",
           }
           && ReferenceEquals(publishException.InnerException, publishException.OriginalFailure)
           && publishException.CompensationFailures.IsEmpty,
        "publish failure must preserve the exact original error and report successful persistent compensation");
    AssertBehaviorStateUnchanged(
        faultBaseline,
        await CaptureBehaviorStateAsync(importOm),
        "publish failure with successful compensation must restore metadata and binding rows while retaining the exact registry and readiness state");

    var failureBarrierReached = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    var releaseFailureBarrier = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    importOm.Runtime.BehaviorImportFaults.AfterPersistentCommitAsync = async () =>
    {
        failureBarrierReached.SetResult();
        await releaseFailureBarrier.Task;
    };
    importOm.Runtime.BehaviorImportFaults.BeforeRegistryPublish = () =>
        throw new InvalidOperationException("simulated gated publication failure");
    var failingImport = importOm.ImportBehaviorManifestJsonAsync(changedJson, fullCallbacks);
    await failureBarrierReached.Task;
    var failureReader = importOm.GetBehaviorCatalogAsync();
    await Task.Yield();
    Assert(!failureReader.IsCompleted,
        "readers must remain gated between persistent commit and failed registry publication");
    releaseFailureBarrier.SetResult();
    await ExpectExceptionAsync<BehaviorImportException>(
        async () => await failingImport,
        "gated publication failure should compensate before readers resume");
    var failureReaderCatalog = await failureReader;
    importOm.Runtime.BehaviorImportFaults.Reset();
    Assert(faultBaseline.CanonicalCatalog.SequenceEqual(BehaviorManifestJsonCodec.Encode(failureReaderCatalog))
           && faultBaseline.Metadata.SequenceEqual(CaptureBehaviorMetadata(failureReaderCatalog))
           && faultBaseline.Readiness.SequenceEqual(CaptureBehaviorReadiness(failureReaderCatalog)),
        "reader released after failed publication must observe the complete compensated pre-import catalog and readiness, never a split state");
    AssertBehaviorStateUnchanged(
        faultBaseline,
        await CaptureBehaviorStateAsync(importOm),
        "gated publish failure must finish compensation before exposing unchanged metadata, binding rows, registry and readiness");

    importOm.Runtime.BehaviorImportFaults.BeforeRegistryPublish = () =>
        throw new InvalidOperationException("simulated double publication failure");
    importOm.Runtime.BehaviorImportFaults.BeforePersistentCompensationAsync = () =>
        Task.FromException(new InvalidOperationException("simulated compensation failure"));
    var doubleFailure = await CaptureExceptionAsync<BehaviorImportException>(
        () => importOm.ImportBehaviorManifestJsonAsync(changedJson, fullCallbacks),
        "publish plus persistent compensation failure should surface as a structured import exception");
    importOm.Runtime.BehaviorImportFaults.Reset();
    Assert(doubleFailure.Message == "Behavior registry publication failed and persistent compensation was incomplete."
           && doubleFailure.OriginalFailure is InvalidOperationException
           {
               Message: "simulated double publication failure",
           }
           && ReferenceEquals(doubleFailure.InnerException, doubleFailure.OriginalFailure)
           && doubleFailure.CompensationFailures.Length == 1
           && doubleFailure.CompensationFailures[0] is InvalidOperationException
           {
               Message: "simulated compensation failure",
           },
        "publish plus compensation failure must retain the exact original and compensation errors without claiming restoration");
    var incompleteCompensationState = await CaptureBehaviorStateAsync(importOm);
    Assert(incompleteCompensationState.Metadata.SequenceEqual(CaptureBehaviorMetadata(changedCatalog))
           && incompleteCompensationState.Bindings.SequenceEqual(faultBaseline.Bindings)
           && incompleteCompensationState.RegistryBindings.SequenceEqual(faultBaseline.RegistryBindings)
           && ReferenceEquals(incompleteCompensationState.RegistrySnapshot, faultBaseline.RegistrySnapshot)
           && incompleteCompensationState.Readiness.SequenceEqual(faultBaseline.Readiness)
           && !incompleteCompensationState.CanonicalCatalog.SequenceEqual(faultBaseline.CanonicalCatalog),
        "failed persistent compensation must expose the exact changed metadata with pre-import bindings, registry and readiness instead of claiming recovery");

    // Restore a known pre-state after the deliberately failed compensation.
    var restored = await importOm.ImportBehaviorManifestJsonAsync(importJson, fullCallbacks, new BehaviorImportOptions(RequireReady: true));
    Assert(restored.Applied, "a later strict import should recover the deliberately uncompensated test state");
    importedConditionalWhenRan = false;
    importedConditionalThenRan = false;
    importedCustomValidatorRan = false;
    var restoredValidation = await importOm.ValidateObjectAsync("import:1");
    Assert(restoredValidation.Valid
           && importedConditionalWhenRan
           && importedConditionalThenRan
           && importedCustomValidatorRan,
        "legal imported conditional and custom constraints must execute when, then and validator callbacks without being skipped");

    var successfulPublicationPre = await CaptureBehaviorStateAsync(importOm);
    var commitReached = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    var releasePublish = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    importOm.Runtime.BehaviorImportFaults.AfterPersistentCommitAsync = async () =>
    {
        commitReached.SetResult();
        await releasePublish.Task;
    };
    var concurrentImport = importOm.ImportBehaviorManifestJsonAsync(changedJson, fullCallbacks, new BehaviorImportOptions(RequireReady: true));
    await commitReached.Task;
    var concurrentReader = importOm.GetBehaviorCatalogAsync();
    await Task.Yield();
    Assert(!concurrentReader.IsCompleted,
        "catalog readers must wait while persistence is committed but the staged registry is not published");
    releasePublish.SetResult();
    var concurrentResult = await concurrentImport;
    var concurrentCatalog = await concurrentReader;
    importOm.Runtime.BehaviorImportFaults.Reset();
    var successfulPublicationPost = await CaptureBehaviorStateAsync(importOm);
    var expectedSuccessfulRegistry = successfulPublicationPre.RegistryBindings
        .Select(entry => entry is
        {
            Kind: BehaviorCatalogKind.Interceptor,
            OwnerClass: "ImportOwner",
            Name: "portable_operation",
            Phase: "before",
            Seq: 7,
        }
            ? entry with { Description = "portable before changed" }
            : entry)
        .ToImmutableArray();
    Assert(concurrentResult.Applied
           && BehaviorManifestJsonCodec.Encode(concurrentCatalog).SequenceEqual(Encoding.UTF8.GetBytes(changedJson))
           && CaptureBehaviorMetadata(concurrentCatalog).SequenceEqual(successfulPublicationPost.Metadata)
           && CaptureBehaviorReadiness(concurrentCatalog).SequenceEqual(successfulPublicationPost.Readiness)
           && successfulPublicationPost.Metadata.SequenceEqual(CaptureBehaviorMetadata(changedCatalog))
           && successfulPublicationPost.Bindings.SequenceEqual(successfulPublicationPre.Bindings)
           && successfulPublicationPost.RegistryBindings.SequenceEqual(expectedSuccessfulRegistry)
           && !ReferenceEquals(successfulPublicationPost.RegistrySnapshot, successfulPublicationPre.RegistrySnapshot)
           && successfulPublicationPost.Readiness.All(entry => entry.Readiness == BehaviorReadiness.Ready),
        "a gated concurrent reader must observe the exact complete post-import catalog/readiness while publication atomically swaps the staged registry");

    var publicationConflictPre = successfulPublicationPost;
    var conflictCommitReached = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    var releaseConflictPublish = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    importOm.Runtime.BehaviorImportFaults.AfterPersistentCommitAsync = async () =>
    {
        conflictCommitReached.SetResult();
        await releaseConflictPublish.Task;
    };
    var conflictingImport = importOm.ImportBehaviorManifestJsonAsync(
        importJson,
        fullCallbacks,
        new BehaviorImportOptions(RequireReady: true));
    await conflictCommitReached.Task;
    var conflictReader = importOm.GetBehaviorCatalogAsync();
    await Task.Yield();
    Assert(!conflictReader.IsCompleted,
        "catalog readers must remain gated while a concurrent registry write forces import compensation");

    Func<OmOperationContext, IReadOnlyDictionary<string, object?>, ValueTask<IReadOnlyList<MutationSpec>>> concurrentOperation =
        (_, _) => ValueTask.FromResult<IReadOnlyList<MutationSpec>>([]);
    importOm.Runtime.Registry.RegisterOperation(
        "ImportOwner",
        "portable_operation",
        "concurrent:operation",
        concurrentOperation);
    var concurrentWriterSnapshot = importOm.Runtime.Registry.CaptureSnapshot();
    Assert(concurrentWriterSnapshot.Operations.TryGetValue(("ImportOwner", "portable_operation"), out var concurrentRegistration)
           && concurrentRegistration.BindingId == "concurrent:operation"
           && ReferenceEquals(concurrentRegistration.Callback, concurrentOperation),
        "the synchronous registry writer must publish without waiting for the async behavior gate");

    releaseConflictPublish.SetResult();
    var conflictException = await CaptureExceptionAsync<BehaviorImportException>(
        async () => await conflictingImport,
        "a concurrent registry write must reject staged import publication");
    var conflictReaderCatalog = await conflictReader;
    importOm.Runtime.BehaviorImportFaults.Reset();
    var publicationConflictPost = await CaptureBehaviorStateAsync(importOm);
    var conflictedActionReadiness = publicationConflictPost.Readiness.Single(entry => entry is
    {
        Kind: BehaviorCatalogKind.Operation,
        OwnerClass: "ImportOwner",
        Name: "portable_operation",
        Slot: BehaviorCatalogCallbackSlot.Handler,
    });
    Assert(conflictException.Message == "Behavior registry publication failed; persistent state was restored."
           && conflictException.OriginalFailure is BehaviorRegistryPublicationConflictException
           {
               Message: "Behavior registry changed after import staging; staged publication was rejected.",
           }
           && conflictException.CompensationFailures.IsEmpty,
        "registry CAS mismatch must surface as a deterministic structured publication failure with successful compensation");
    Assert(publicationConflictPost.Metadata.SequenceEqual(publicationConflictPre.Metadata)
           && publicationConflictPost.Bindings.SequenceEqual(publicationConflictPre.Bindings)
           && ReferenceEquals(publicationConflictPost.RegistrySnapshot, concurrentWriterSnapshot)
           && publicationConflictPost.RegistrySnapshot.Operations.TryGetValue(("ImportOwner", "portable_operation"), out var preservedRegistration)
           && preservedRegistration.BindingId == "concurrent:operation"
           && ReferenceEquals(preservedRegistration.Callback, concurrentOperation)
           && conflictedActionReadiness is
           {
               BindingId: "import:operation",
               Readiness: BehaviorReadiness.Unresolved,
           },
        "publication conflict compensation must restore persistent state, preserve the concurrent writer and derive unresolved readiness from its actual identity");
    Assert(CaptureBehaviorMetadata(conflictReaderCatalog).SequenceEqual(publicationConflictPost.Metadata)
           && CaptureBehaviorReadiness(conflictReaderCatalog).SequenceEqual(publicationConflictPost.Readiness),
        "the gated reader must resume only after compensation and observe persistent old bindings with the preserved concurrent registry identity");

    var originalOperation = fullCallbacks.Operations.Single(binding => binding.BindingId == "import:operation");
    importOm.Runtime.Registry.RegisterOperation(
        "ImportOwner",
        "portable_operation",
        originalOperation.BindingId,
        originalOperation.Callback);
    var reboundOperation = (await importOm.GetBehaviorCatalogAsync()).Behaviors.Single(entry => entry is
    {
        Kind: BehaviorCatalogKind.Operation,
        OwnerClass: "ImportOwner",
        Name: "portable_operation",
    });
    Assert(reboundOperation.Callbacks.Single().Readiness == BehaviorReadiness.Ready,
        "restoring the persistent binding identity after the conflict should derive ready from the new registry snapshot");

    var sparseCatalog = new BehaviorCatalog([
        new BehaviorCatalogEntry(
            BehaviorCatalogKind.Interceptor, "ImportOwner", "portable_operation", null, null, "sparse eleven", "before", 11,
            [new(BehaviorCatalogCallbackSlot.Handler, "import:interceptor:11", BehaviorReadiness.Ready)]),
    ]);
    var sparseCallbacks = new BehaviorCallbackBindingSet(
        interceptors: [new("import:interceptor:11", _ => ValueTask.CompletedTask)]);
    var sparseResult = await importOm.ImportBehaviorManifestJsonAsync(
        Encoding.UTF8.GetString(BehaviorManifestJsonCodec.Encode(sparseCatalog)),
        sparseCallbacks,
        new BehaviorImportOptions(RequireReady: true));
    await importOm.AddInterceptorAsync("ImportOwner", "portable_operation", "before", _ => ValueTask.CompletedTask, "after sparse import");
    var sparseState = await CaptureBehaviorStateAsync(importOm);
    var sparseEntries = (await importOm.GetBehaviorCatalogAsync()).Behaviors
        .Where(entry => entry.Kind == BehaviorCatalogKind.Interceptor
                        && entry.OwnerClass == "ImportOwner"
                        && entry.Name == "portable_operation"
                        && entry.InterceptorPhase == "before")
        .OrderBy(entry => entry.InterceptorSeq)
        .ToArray();
    var sparseEleven = sparseEntries.Single(entry => entry.InterceptorSeq == 11);
    var sparseTwelve = sparseEntries.Single(entry => entry.InterceptorSeq == 12);
    Assert(sparseResult.Applied
           && sparseEntries.Select(entry => entry.InterceptorSeq).SequenceEqual(new int?[] { 7, 11, 12 })
           && sparseEleven.Description == "sparse eleven"
           && sparseEleven.Callbacks.Single() is
           {
               BindingId: "import:interceptor:11",
               Readiness: BehaviorReadiness.Ready,
           }
           && sparseTwelve.Description == "after sparse import"
           && sparseTwelve.Callbacks.Single() is
           {
               BindingId: null,
               Readiness: BehaviorReadiness.Unbound,
           }
           && sparseState.Bindings.Any(row => row is
           {
               Key:
               {
                   BehaviorKind: BehaviorKind.Interceptor,
                   OwnerClass: "ImportOwner",
                   BehaviorName: "portable_operation",
                   CallbackSlot: BehaviorCallbackSlot.Handler,
                   Phase: "before",
                   Seq: 11,
               },
               BindingId: "import:interceptor:11",
           })
           && !sparseState.Bindings.Any(row => row.Key is
           {
               BehaviorKind: BehaviorKind.Interceptor,
               OwnerClass: "ImportOwner",
               BehaviorName: "portable_operation",
               Phase: "before",
               Seq: 12,
           })
           && importOm.Runtime.Registry.TryGetInterceptor("ImportOwner", "portable_operation", "before", 11, out var interceptorEleven)
           && interceptorEleven.BindingId == "import:interceptor:11"
           && interceptorEleven.Description == "sparse eleven"
           && importOm.Runtime.Registry.TryGetInterceptor("ImportOwner", "portable_operation", "before", 12, out var interceptorTwelve)
           && interceptorTwelve.BindingId is null
           && interceptorTwelve.Description == "after sparse import",
        "sparse interceptor import must preserve exact owner/operation/phase/seq metadata, binding and registry identity, then allocate max plus one without overwriting");

    var unresolvedSparseCatalog = new BehaviorCatalog([
        new BehaviorCatalogEntry(
            BehaviorCatalogKind.Interceptor, "ImportOwner", "portable_operation", null, null, "unresolved sparse twenty-one", "before", 21,
            [new(BehaviorCatalogCallbackSlot.Handler, "import:interceptor:21", BehaviorReadiness.Unresolved)]),
    ]);
    var unresolvedSparseResult = await importOm.ImportBehaviorManifestJsonAsync(
        Encoding.UTF8.GetString(BehaviorManifestJsonCodec.Encode(unresolvedSparseCatalog)));
    await importOm.AddInterceptorAsync("ImportOwner", "portable_operation", "before", _ => ValueTask.CompletedTask, "after unresolved sparse import");
    var unresolvedSparseEntries = (await importOm.GetBehaviorCatalogAsync()).Behaviors
        .Where(entry => entry.Kind == BehaviorCatalogKind.Interceptor
                        && entry.OwnerClass == "ImportOwner"
                        && entry.Name == "portable_operation"
                        && entry.InterceptorPhase == "before")
        .OrderBy(entry => entry.InterceptorSeq)
        .ToArray();
    var unresolvedTwentyOne = unresolvedSparseEntries.Single(entry => entry.InterceptorSeq == 21);
    var unresolvedTwentyTwo = unresolvedSparseEntries.Single(entry => entry.InterceptorSeq == 22);
    Assert(unresolvedSparseResult is
           {
               Applied: true,
               Unresolved.Length: 1,
           }
           && unresolvedTwentyOne.Callbacks.Single() is
           {
               BindingId: "import:interceptor:21",
               Readiness: BehaviorReadiness.Unresolved,
           }
           && unresolvedTwentyTwo.Callbacks.Single() is
           {
               BindingId: null,
               Readiness: BehaviorReadiness.Unbound,
           }
           && !importOm.Runtime.Registry.TryGetInterceptor("ImportOwner", "portable_operation", "before", 21, out _)
           && importOm.Runtime.Registry.TryGetInterceptor("ImportOwner", "portable_operation", "before", 22, out var unresolvedSparseNative)
           && unresolvedSparseNative.BindingId is null
           && unresolvedSparseNative.Description == "after unresolved sparse import",
        "native interceptor allocation must consider unresolved imported metadata sequences, not only ready registry entries");
}

var importPersistenceDirectory = Path.Combine(Path.GetTempPath(), $"cozo-om-import-{Guid.NewGuid():N}");
Directory.CreateDirectory(importPersistenceDirectory);
var importPersistencePath = Path.Combine(importPersistenceDirectory, "import.db");
try
{
    const string restartOwner = "RestartImportBase";
    const string restartChild = "RestartImportChild";
    const string restartObjectId = "restart:child";

    BehaviorCatalog RestartCatalog(Func<string, BehaviorReadiness> readiness) => new([
        new BehaviorCatalogEntry(
            BehaviorCatalogKind.Constraint, restartOwner, "restart_conditional", "conditional", "restart conditional", null, null, null,
            [
                new(BehaviorCatalogCallbackSlot.When, "restart:constraint:when", readiness("restart:constraint:when")),
                new(BehaviorCatalogCallbackSlot.Then, "restart:constraint:then", readiness("restart:constraint:then")),
            ]),
        new BehaviorCatalogEntry(
            BehaviorCatalogKind.Constraint, restartOwner, "restart_custom", "custom", "restart custom", null, null, null,
            [new(BehaviorCatalogCallbackSlot.Validator, "restart:constraint:validator", readiness("restart:constraint:validator"))]),
        new BehaviorCatalogEntry(
            BehaviorCatalogKind.ComputedProp, restartOwner, "restart_computed_prop", null, null, "restart computedProp", null, null,
            [new(BehaviorCatalogCallbackSlot.Compute, "restart:computedProp", readiness("restart:computedProp"))]),
        new BehaviorCatalogEntry(
            BehaviorCatalogKind.Operation, restartOwner, "restart_operation", null, null, "restart operation", null, null,
            [new(BehaviorCatalogCallbackSlot.Handler, "restart:operation", readiness("restart:operation"))]),
        new BehaviorCatalogEntry(
            BehaviorCatalogKind.Mutation, restartOwner, "restart_mutation", null, null, "restart mutation", null, null,
            [new(BehaviorCatalogCallbackSlot.Executor, "restart:mutation", readiness("restart:mutation"))]),
        new BehaviorCatalogEntry(
            BehaviorCatalogKind.Interceptor, restartOwner, "restart_operation", null, null, "restart after", "after", 7,
            [new(BehaviorCatalogCallbackSlot.Handler, "restart:interceptor:after:7", readiness("restart:interceptor:after:7"))]),
    ]);

    BehaviorCallbackBindingSet RestartCallbacks(
        List<string> events,
        bool includeThen = true,
        bool includeInterceptor = true) => new(
        constraints:
        [
            new("restart:constraint:when", _ =>
            {
                events.Add("constraint:when");
                return ValueTask.FromResult(true);
            }),
            .. includeThen
                ? [new BehaviorConstraintCallbackBinding("restart:constraint:then", _ =>
                {
                    events.Add("constraint:then");
                    return ValueTask.FromResult(true);
                })]
                : Array.Empty<BehaviorConstraintCallbackBinding>(),
        ],
        validators:
        [
            new("restart:constraint:validator", _ =>
            {
                events.Add("constraint:validator");
                return ValueTask.FromResult<string?>(null);
            }),
        ],
        computedProp:
        [
            new("restart:computedProp", _ =>
            {
                events.Add("computedProp");
                return ValueTask.FromResult<object?>("restart-computedProp-value");
            }),
        ],
        operations:
        [
            new("restart:operation", (_, _) =>
            {
                events.Add("operation");
                return ValueTask.FromResult<IReadOnlyList<MutationSpec>>(
                [
                    new MutationSpec(
                        "restart_mutation",
                        new Dictionary<string, object?> { ["value"] = "operation-mutated" }),
                ]);
            }),
        ],
        mutations:
        [
            new("restart:mutation", async (ctx, parameters) =>
            {
                events.Add("mutation");
                await ctx.SetFieldValueAsync("restart_effect", parameters["value"]);
            }),
        ],
        interceptors: includeInterceptor
            ? [new BehaviorInterceptorCallbackBinding("restart:interceptor:after:7", _ =>
            {
                events.Add("interceptor:after:7");
                return ValueTask.CompletedTask;
            })]
            : []);

    void AssertRestartCatalog(
        BehaviorCatalog actual,
        Func<string, BehaviorReadiness> readiness,
        string stage)
    {
        Assert(
            BehaviorManifestJsonCodec.Encode(actual).SequenceEqual(
                BehaviorManifestJsonCodec.Encode(RestartCatalog(readiness))),
            $"{stage} must preserve exact behavior metadata, callback slots, binding ids, interceptor phase/seq and readiness");
    }

    var restartManifest = BehaviorManifestJsonCodec.Encode(RestartCatalog(_ => BehaviorReadiness.Ready));
    using (var persistentImportDb = new CozoDb(engine: "sqlite", path: importPersistencePath))
    {
        var persistentImportOm = new CozoOm(persistentImportDb);
        await persistentImportOm.InitSchemaAsync();
        await persistentImportOm.DefineClassAsync(restartOwner, "Restart import base");
        await persistentImportOm.DefineClassAsync(restartChild, "Restart import child", restartOwner);
        await persistentImportOm.DefineFieldAsync(restartOwner, "restart_effect", OmValueType.String);
        await persistentImportOm.CreateObjectAsync(restartObjectId, restartChild, "Restart import child entity");
        await persistentImportOm.SetFieldValueAsync(restartObjectId, "restart_effect", "initial");

        var beforeImport = await persistentImportOm.GetBehaviorCatalogAsync();
        Assert(
            BehaviorManifestJsonCodec.Encode(beforeImport).SequenceEqual(
                BehaviorManifestJsonCodec.Encode(new BehaviorCatalog([]))),
            "before-import catalog must contain no behavior metadata or callback binding identity");

        var postImportEvents = new List<string>();
        var result = await persistentImportOm.ImportBehaviorManifestJsonAsync(
            Encoding.UTF8.GetString(restartManifest),
            RestartCallbacks(postImportEvents),
            new BehaviorImportOptions(RequireReady: true));
        Assert(result is { Applied: true, Diagnostics.Length: 0, Unresolved.Length: 0 },
            "strict persistent import must apply the complete canonical manifest without diagnostics");
        AssertRestartCatalog(
            await persistentImportOm.GetBehaviorCatalogAsync(),
            _ => BehaviorReadiness.Ready,
            "post-import catalog");

        var postImportValidation = await persistentImportOm.ValidateObjectAsync(restartObjectId);
        Assert(postImportValidation.Valid
               && postImportEvents.SequenceEqual(["constraint:when", "constraint:then", "constraint:validator"]),
            "post-import validation must execute conditional when+then and custom validator callbacks");
        postImportEvents.Clear();
        Assert(AsString(await persistentImportOm.GetFieldValueAsync(restartObjectId, "restart_computed_prop")) == "restart-computedProp-value"
               && postImportEvents.SequenceEqual(["computedProp"]),
            "post-import inherited computedProp lookup must execute the imported callback");
        postImportEvents.Clear();
        await persistentImportOm.ExecuteOperationAsync(restartObjectId, "restart_operation");
        var postImportEffect = AsString(await persistentImportOm.GetFieldValueAsync(restartObjectId, "restart_effect"));
        Assert(postImportEvents.SequenceEqual(
                   ["operation", "mutation", "constraint:when", "constraint:then", "constraint:validator", "interceptor:after:7"])
               && postImportEffect == "operation-mutated",
            $"post-import inherited operation must execute its mutation and exact after/7 interceptor; events={string.Join(",", postImportEvents)} effect={postImportEffect}");
    }
    using (var reopenedImportDb = new CozoDb(engine: "sqlite", path: importPersistencePath))
    {
        var reopenedImportOm = new CozoOm(reopenedImportDb);
        await reopenedImportOm.InitSchemaAsync();
        AssertRestartCatalog(
            await reopenedImportOm.GetBehaviorCatalogAsync(),
            _ => BehaviorReadiness.Unresolved,
            "post-restart catalog");

        await ExpectUnresolvedAsync(
            async () => { await ConstraintLogic.ValidateConstraintsAsync(reopenedImportOm.Runtime, restartObjectId, ["conditional"]); },
            BehaviorCatalogKind.Constraint,
            restartOwner,
            $"constraint:{restartOwner}/restart_conditional",
            BehaviorCatalogCallbackSlot.When,
            "restart:constraint:when");
        await ExpectUnresolvedAsync(
            async () => { await ConstraintLogic.ValidateConstraintsAsync(reopenedImportOm.Runtime, restartObjectId, ["custom"]); },
            BehaviorCatalogKind.Constraint,
            restartOwner,
            $"constraint:{restartOwner}/restart_custom",
            BehaviorCatalogCallbackSlot.Validator,
            "restart:constraint:validator");
        await ExpectUnresolvedAsync(
            async () => { await reopenedImportOm.GetFieldValueAsync(restartObjectId, "restart_computed_prop"); },
            BehaviorCatalogKind.ComputedProp,
            restartOwner,
            $"computedProp:{restartOwner}/restart_computed_prop",
            BehaviorCatalogCallbackSlot.Compute,
            "restart:computedProp");
        await ExpectUnresolvedAsync(
            () => reopenedImportOm.ExecuteOperationAsync(restartObjectId, "restart_operation"),
            BehaviorCatalogKind.Operation,
            restartOwner,
            $"operation:{restartOwner}/restart_operation",
            BehaviorCatalogCallbackSlot.Handler,
            "restart:operation");
        await ExpectUnresolvedAsync(
            () => reopenedImportOm.ExecuteMutationsAsync(
                restartObjectId,
                [new MutationSpec("restart_mutation", new Dictionary<string, object?> { ["value"] = "must-not-commit" })]),
            BehaviorCatalogKind.Mutation,
            restartOwner,
            $"mutation:{restartOwner}/restart_mutation",
            BehaviorCatalogCallbackSlot.Executor,
            "restart:mutation");
        Assert(AsString(await reopenedImportOm.GetFieldValueAsync(restartObjectId, "restart_effect")) == "operation-mutated",
            "post-restart fail-closed operation and mutation paths must not commit effects");

        var partialEvents = new List<string>();
        var partialRebind = await reopenedImportOm.ImportBehaviorManifestJsonAsync(
            Encoding.UTF8.GetString(restartManifest),
            RestartCallbacks(partialEvents, includeThen: false, includeInterceptor: false));
        Assert(partialRebind.Applied
               && partialRebind.Unresolved.Select(item => (item.Slot, item.BindingId)).ToHashSet().SetEquals(
               [
                   (BehaviorCatalogCallbackSlot.Then, "restart:constraint:then"),
                   (BehaviorCatalogCallbackSlot.Handler, "restart:interceptor:after:7"),
               ]),
            "partial rebind must report the exact missing conditional then and interceptor slots");
        AssertRestartCatalog(
            await reopenedImportOm.GetBehaviorCatalogAsync(),
            bindingId => bindingId is "restart:constraint:then" or "restart:interceptor:after:7"
                ? BehaviorReadiness.Unresolved
                : BehaviorReadiness.Ready,
            "partial-rebind catalog");
        await ExpectUnresolvedAsync(
            async () => { await ConstraintLogic.ValidateConstraintsAsync(reopenedImportOm.Runtime, restartObjectId, ["conditional"]); },
            BehaviorCatalogKind.Constraint,
            restartOwner,
            $"constraint:{restartOwner}/restart_conditional",
            BehaviorCatalogCallbackSlot.Then,
            "restart:constraint:then");
        Assert(partialEvents.Count == 0,
            "partial constraint rebind must resolve every slot before executing the ready when callback");
        await ExpectUnresolvedAsync(
            () => reopenedImportOm.ExecuteOperationAsync(restartObjectId, "restart_operation"),
            BehaviorCatalogKind.Interceptor,
            restartOwner,
            $"interceptor:{restartOwner}/restart_operation/after/7",
            BehaviorCatalogCallbackSlot.Handler,
            "restart:interceptor:after:7",
            "after",
            7);
        Assert(partialEvents.Count == 0
               && AsString(await reopenedImportOm.GetFieldValueAsync(restartObjectId, "restart_effect")) == "operation-mutated",
            "unresolved inherited interceptor must fail before operation, mutation, interceptor callbacks or writes");

        var fullRebindEvents = new List<string>();
        var fullRebind = await reopenedImportOm.ImportBehaviorManifestJsonAsync(
            Encoding.UTF8.GetString(restartManifest),
            RestartCallbacks(fullRebindEvents),
            new BehaviorImportOptions(RequireReady: true));
        Assert(fullRebind is { Applied: true, Diagnostics.Length: 0, Unresolved.Length: 0 },
            "full exact-id rebind must satisfy requireReady without diagnostics");
        AssertRestartCatalog(
            await reopenedImportOm.GetBehaviorCatalogAsync(),
            _ => BehaviorReadiness.Ready,
            "full-rebind catalog");

        var reboundValidation = await reopenedImportOm.ValidateObjectAsync(restartObjectId);
        Assert(reboundValidation.Valid
               && fullRebindEvents.SequenceEqual(["constraint:when", "constraint:then", "constraint:validator"]),
            "full rebind must execute conditional when+then and custom validator callbacks");
        fullRebindEvents.Clear();
        Assert(AsString(await reopenedImportOm.GetFieldValueAsync(restartObjectId, "restart_computed_prop")) == "restart-computedProp-value"
               && fullRebindEvents.SequenceEqual(["computedProp"]),
            "full rebind must execute the inherited computedProp callback");
        fullRebindEvents.Clear();
        await reopenedImportOm.ExecuteOperationAsync(restartObjectId, "restart_operation");
        var fullRebindEffect = AsString(await reopenedImportOm.GetFieldValueAsync(restartObjectId, "restart_effect"));
        Assert(fullRebindEvents.SequenceEqual(
                   ["operation", "mutation", "constraint:when", "constraint:then", "constraint:validator", "interceptor:after:7"])
               && fullRebindEffect == "operation-mutated",
            $"full rebind must execute inherited operation, returned mutation and exact after/7 interceptor callbacks; events={string.Join(",", fullRebindEvents)} effect={fullRebindEffect}");
        fullRebindEvents.Clear();
        await reopenedImportOm.ExecuteMutationsAsync(
            restartObjectId,
            [new MutationSpec("restart_mutation", new Dictionary<string, object?> { ["value"] = "direct-mutated" })]);
        Assert(fullRebindEvents.SequenceEqual(
                   ["mutation", "constraint:when", "constraint:then", "constraint:validator"])
               && AsString(await reopenedImportOm.GetFieldValueAsync(restartObjectId, "restart_effect")) == "direct-mutated",
            "full rebind must execute the representative direct mutation path");
    }
}
finally
{
    Directory.Delete(importPersistenceDirectory, recursive: true);
}

var yamlCatalog = new BehaviorCatalog([
    new BehaviorCatalogEntry(
        BehaviorCatalogKind.Operation,
        "YamlOwner",
        "yaml_operation",
        null,
        null,
        "null",
        null,
        null,
        [new(BehaviorCatalogCallbackSlot.Handler, "yaml:operation", BehaviorReadiness.Ready)]),
    new BehaviorCatalogEntry(
        BehaviorCatalogKind.ComputedProp,
        "YamlOwner",
        "yaml_computed",
        null,
        null,
        "YAML computedProp",
        null,
        null,
        [new(BehaviorCatalogCallbackSlot.Compute, "yaml:computedProp", BehaviorReadiness.Ready)]),
]);
var yamlManifest = """
    version: 1
    ignoredBoolean: true
    behaviors:
      - kind: operation
        ownerClass: YamlOwner
        name: yaml_operation
        constraintKind: null
        message: null
        description: "null"
        interceptorPhase: null
        interceptorSeq: null
        callbacks:
          - slot: handler
            bindingId: yaml:operation
            readiness: ready
      - kind: computedProp
        ownerClass: YamlOwner
        name: yaml_computed
        constraintKind: null
        message: null
        description: YAML computedProp
        interceptorPhase: null
        interceptorSeq: null
        callbacks:
          - slot: compute
            bindingId: yaml:computedProp
            readiness: ready
    """;
var yamlDecoded = BehaviorManifestYamlAdapter.Decode(yamlManifest);
var jsonDecoded = BehaviorManifestJsonCodec.Decode(BehaviorManifestJsonCodec.Encode(yamlCatalog));
Assert(yamlDecoded.Success && jsonDecoded.Success
       && BehaviorManifestJsonCodec.Encode(yamlDecoded.Catalog!).SequenceEqual(
           BehaviorManifestJsonCodec.Encode(jsonDecoded.Catalog!)),
    "equivalent YAML and JSON must normalize through the same canonical catalog codec");

var semanticYaml = """
    version: 1
    behaviors:
      - kind: unknown
        ownerClass: YamlOwner
        name: invalid
        constraintKind: null
        message: null
        description: null
        interceptorPhase: null
        interceptorSeq: null
        callbacks: []
    """;
var semanticJson = """
    {"version":1,"behaviors":[{"kind":"unknown","ownerClass":"YamlOwner","name":"invalid","constraintKind":null,"message":null,"description":null,"interceptorPhase":null,"interceptorSeq":null,"callbacks":[]}]}
    """;
var yamlSemanticDiagnostics = BehaviorManifestYamlAdapter.Decode(semanticYaml).Diagnostics;
var jsonSemanticDiagnostics = BehaviorManifestJsonCodec.Decode(semanticJson).Diagnostics;
Assert(yamlSemanticDiagnostics.SequenceEqual(jsonSemanticDiagnostics)
       && yamlSemanticDiagnostics.Single().Code == "OMM1101",
    "YAML semantic failures must retain the canonical JSON diagnostic code, path and message");

var crossKindMetadataYaml = """
    version: 1
    behaviors:
      - kind: operation
        ownerClass: YamlOwner
        name: invalid_cross_kind
        constraintKind: custom
        message: forbidden
        description: allowed
        interceptorPhase: null
        interceptorSeq: null
        callbacks: []
    """;
var crossKindMetadataJson = """
    {"version":1,"behaviors":[{"kind":"operation","ownerClass":"YamlOwner","name":"invalid_cross_kind","constraintKind":"custom","message":"forbidden","description":"allowed","interceptorPhase":null,"interceptorSeq":null,"callbacks":[]}]}
    """;
var yamlCrossKindDiagnostics = BehaviorManifestYamlAdapter.Decode(crossKindMetadataYaml).Diagnostics;
var jsonCrossKindDiagnostics = BehaviorManifestJsonCodec.Decode(crossKindMetadataJson).Diagnostics;
Assert(yamlCrossKindDiagnostics.SequenceEqual(jsonCrossKindDiagnostics)
       && yamlCrossKindDiagnostics.SequenceEqual(
       [
           new BehaviorManifestDiagnostic("OMM1203", "$.behaviors[0].constraintKind", "Property 'constraintKind' must be null for behavior kind 'operation'."),
           new BehaviorManifestDiagnostic("OMM1203", "$.behaviors[0].message", "Property 'message' must be null for behavior kind 'operation'."),
       ]),
    "equivalent YAML and JSON cross-kind metadata must use the same canonical semantic diagnostics");

HarnessDiagnostics.Start("YAML portability matrix");

using (var yamlImportDb = new CozoDb(engine: "mem", path: ""))
using (var jsonImportDb = new CozoDb(engine: "mem", path: ""))
{
    var yamlImportOm = new CozoOm(yamlImportDb);
    var jsonImportOm = new CozoOm(jsonImportDb);
    await yamlImportOm.InitSchemaAsync();
    await jsonImportOm.InitSchemaAsync();
    await yamlImportOm.DefineClassAsync("YamlOwner", "YAML owner");
    await jsonImportOm.DefineClassAsync("YamlOwner", "YAML owner");
    await yamlImportOm.CreateObjectAsync("yaml:1", "YamlOwner", "YAML entity");
    await jsonImportOm.CreateObjectAsync("yaml:1", "YamlOwner", "YAML entity");

    var yamlActionRan = false;
    var yamlCallbacks = new BehaviorCallbackBindingSet(operations:
    [
        new("yaml:operation", (_, _) =>
        {
            yamlActionRan = true;
            return ValueTask.FromResult<IReadOnlyList<MutationSpec>>([]);
        }),
    ]);
    var jsonCallbacks = new BehaviorCallbackBindingSet(operations:
    [
        new("yaml:operation", (_, _) => ValueTask.FromResult<IReadOnlyList<MutationSpec>>([])),
    ]);
    var yamlImportResult = await BehaviorManifestYamlAdapter.ImportAsync(
        yamlImportOm,
        yamlManifest,
        yamlCallbacks);
    var jsonImportResult = await jsonImportOm.ImportBehaviorManifestJsonAsync(
        Encoding.UTF8.GetString(BehaviorManifestJsonCodec.Encode(yamlCatalog)),
        jsonCallbacks);
    var yamlImportedState = await CaptureBehaviorStateAsync(yamlImportOm);
    var jsonImportedState = await CaptureBehaviorStateAsync(jsonImportOm);
    var expectedYamlDiagnostic = new BehaviorImportDiagnostic(
        "OMI1201",
        "computedProp:YamlOwner/yaml_computed",
        "Binding 'yaml:computedProp' has no supplied typed callback.",
        BehaviorCatalogKind.ComputedProp,
        "YamlOwner",
        "yaml_computed",
        BehaviorCatalogCallbackSlot.Compute,
        "yaml:computedProp");
    var expectedYamlUnresolved = new BehaviorUnresolvedDiagnostic(
        "OMR1001",
        BehaviorCatalogKind.ComputedProp,
        "YamlOwner",
        "computedProp:YamlOwner/yaml_computed",
        BehaviorCatalogCallbackSlot.Compute,
        "yaml:computedProp");
    Assert(yamlImportResult.Applied && jsonImportResult.Applied
           && yamlImportResult.Diagnostics.SequenceEqual(jsonImportResult.Diagnostics)
           && yamlImportResult.Diagnostics.SequenceEqual([expectedYamlDiagnostic])
           && yamlImportResult.Unresolved.SequenceEqual(jsonImportResult.Unresolved)
           && yamlImportResult.Unresolved.SequenceEqual([expectedYamlUnresolved]),
        "equivalent YAML and JSON imports must return the same non-empty structured diagnostics and unresolved identities");
    AssertBehaviorStateEquivalent(
        jsonImportedState,
        yamlImportedState,
        "equivalent YAML and JSON imports must produce identical metadata, binding rows, registry identities, readiness and canonical catalog values");
    var yamlCrossKindState = await CaptureBehaviorStateAsync(yamlImportOm);
    var yamlCrossKindFailure = await BehaviorManifestYamlAdapter.ImportAsync(
        yamlImportOm,
        crossKindMetadataYaml,
        yamlCallbacks);
    Assert(!yamlCrossKindFailure.Applied
           && yamlCrossKindFailure.Unresolved.IsEmpty
           && yamlCrossKindFailure.Diagnostics
               .Select(diagnostic => new BehaviorManifestDiagnostic(diagnostic.Code, diagnostic.Path, diagnostic.Message))
               .SequenceEqual(yamlCrossKindDiagnostics),
        "YAML import must expose canonical cross-kind metadata diagnostics without adapter reinterpretation");
    AssertBehaviorStateUnchanged(
        yamlCrossKindState,
        await CaptureBehaviorStateAsync(yamlImportOm),
        "YAML cross-kind metadata rejection must leave metadata, binding rows, registry and readiness unchanged");
    await yamlImportOm.ExecuteOperationAsync("yaml:1", "yaml_operation");
    Assert(yamlActionRan, "a ready callback imported through YAML must execute through the existing typed registry");

    var invalidConstraintYaml = """
        version: 1
        behaviors:
          - kind: constraint
            ownerClass: YamlOwner
            name: yaml_invalid_custom
            constraintKind: custom
            message: invalid custom shape
            description: null
            interceptorPhase: null
            interceptorSeq: null
            callbacks:
              - slot: when
                bindingId: yaml:invalid:when
                readiness: ready
        """;
    var yamlShapeBefore = BehaviorManifestJsonCodec.Encode(await yamlImportOm.GetBehaviorCatalogAsync());
    var yamlShapeRegistryBefore = yamlImportOm.Runtime.Registry.CaptureSnapshot();
    var yamlShapeFailure = await BehaviorManifestYamlAdapter.ImportAsync(
        yamlImportOm,
        invalidConstraintYaml,
        new BehaviorCallbackBindingSet(constraints:
        [
            new("yaml:invalid:when", _ => ValueTask.FromResult(true)),
        ]));
    var yamlShapeAfter = BehaviorManifestJsonCodec.Encode(await yamlImportOm.GetBehaviorCatalogAsync());
    var yamlShapeDiagnostic = yamlShapeFailure.Diagnostics.Single(diagnostic => diagnostic.Code == "OMI1104");
    Assert(!yamlShapeFailure.Applied
           && yamlShapeDiagnostic is
           {
               Kind: BehaviorCatalogKind.Constraint,
               OwnerClass: "YamlOwner",
               BehaviorName: "yaml_invalid_custom",
               Path: "constraint:YamlOwner/yaml_invalid_custom.callbacks",
           }
           && yamlShapeDiagnostic.Message.Contains("missing slots [validator]", StringComparison.Ordinal)
           && yamlShapeDiagnostic.Message.Contains("extra slots [when]", StringComparison.Ordinal)
           && yamlShapeBefore.SequenceEqual(yamlShapeAfter)
           && ReferenceEquals(yamlShapeRegistryBefore, yamlImportOm.Runtime.Registry.CaptureSnapshot()),
        "YAML constraint kind/slot mismatches must use core preflight and leave catalog and registry unchanged");

    var catalogBeforeUnsafeImport = BehaviorManifestJsonCodec.Encode(await yamlImportOm.GetBehaviorCatalogAsync());
    var unsafeImport = await BehaviorManifestYamlAdapter.ImportAsync(
        yamlImportOm,
        "<<: { version: 1 }\nbehaviors: []",
        yamlCallbacks);
    var catalogAfterUnsafeImport = BehaviorManifestJsonCodec.Encode(await yamlImportOm.GetBehaviorCatalogAsync());
    Assert(!unsafeImport.Applied && unsafeImport.Diagnostics.Single().Code == "OMY1205"
           && catalogBeforeUnsafeImport.SequenceEqual(catalogAfterUnsafeImport),
        "unsafe YAML must return a stable structured diagnostic before any catalog or import effect");
}

static void AssertYamlFailure(
    string yaml,
    string expectedCode,
    string message,
    BehaviorYamlOptions? options = null)
{
    var first = BehaviorManifestYamlAdapter.Decode(yaml, options);
    var second = BehaviorManifestYamlAdapter.Decode(yaml, options);
    Assert(!first.Success && first.Catalog is null
           && first.Diagnostics.Length == 1
           && first.Diagnostics[0].Code == expectedCode
           && first.Diagnostics.SequenceEqual(second.Diagnostics),
        message);
}

AssertYamlFailure(
    "---\nversion: 1\nbehaviors: []\n---\nversion: 1\nbehaviors: []",
    "OMY1201",
    "multiple YAML documents must be rejected deterministically");
AssertYamlFailure(
    "version: !custom 1\nbehaviors: []",
    "OMY1202",
    "explicit YAML tags must be rejected deterministically");
AssertYamlFailure(
    "version: &version 1\nbehaviors: []",
    "OMY1203",
    "YAML anchors must be rejected deterministically");
AssertYamlFailure(
    "version: *missing\nbehaviors: []",
    "OMY1203",
    "YAML aliases must be rejected deterministically");
AssertYamlFailure(
    "<<: { version: 1 }\nbehaviors: []",
    "OMY1205",
    "YAML merge keys must be rejected deterministically");
AssertYamlFailure(
    "version: 1\nversion: 1\nbehaviors: []",
    "OMY1206",
    "duplicate YAML mapping keys must be rejected deterministically");
AssertYamlFailure(
    "? [version]\n: 1\nbehaviors: []",
    "OMY1204",
    "non-scalar YAML mapping keys must be rejected deterministically");
AssertYamlFailure(
    "1: value\nbehaviors: []",
    "OMY1204",
    "non-string YAML mapping keys must be rejected deterministically");
AssertYamlFailure(
    "version: [",
    "OMY1000",
    "malformed YAML must return a stable syntax diagnostic");
AssertYamlFailure(
    yamlManifest,
    "OMY1101",
    "the configured UTF-8 byte limit must be enforced before parsing",
    new BehaviorYamlOptions(MaxUtf8Bytes: 8));
AssertYamlFailure(
    "a: { b: { c: 1 } }",
    "OMY1102",
    "the configured YAML depth limit must be enforced during AST construction",
    new BehaviorYamlOptions(MaxDepth: 2));
AssertYamlFailure(
    "version: 1\nbehaviors: []",
    "OMY1103",
    "the configured YAML node limit must be enforced during AST construction",
    new BehaviorYamlOptions(MaxNodes: 2));
AssertYamlFailure(
    "version: 1\nbehaviors: []",
    "OMY1001",
    "invalid or unbounded YAML options must be rejected",
    new BehaviorYamlOptions(MaxDepth: BehaviorYamlOptions.MaximumDepth + 1));

var invalidUtf8Yaml = BehaviorManifestYamlAdapter.Decode(new byte[] { 0xc3, 0x28 });
Assert(!invalidUtf8Yaml.Success && invalidUtf8Yaml.Diagnostics.Single().Code == "OMY1000",
    "invalid UTF-8 YAML bytes must be rejected before parser effects");
Assert(!typeof(CozoOm).Assembly.GetReferencedAssemblies().Any(reference => reference.Name == "YamlDotNet")
       && typeof(BehaviorManifestYamlAdapter).Assembly.GetReferencedAssemblies().Any(reference => reference.Name == "YamlDotNet"),
    "YamlDotNet must remain isolated in the optional YAML adapter assembly");

using var db = new CozoDb(engine: "mem", path: "");
var om = new CozoOm(db);

await om.InitSchemaAsync();
await om.InitSchemaAsync();

await om.DefineClassAsync("Person", "Person");
await om.DefineClassAsync("Employee", "Employee", parentClass: "Person");
await om.DefineClassAsync("Department", "Department");
await om.DefineClassAsync("UndirectedSource", "Undirected source");
await om.DefineClassAsync("UndirectedTarget", "Undirected target");
await om.DefineFieldAsync("Person", "name", OmValueType.String, required: true);
await om.DefineFieldAsync("Person", "age", OmValueType.Number);
await om.DefineRelationDefAsync("works_in", "Person", "Department");
await om.DefineRelationDefAsync("paired_with", "UndirectedSource", "UndirectedTarget", directed: false);
await om.DefineFieldAliasAsync("Person", "full_name", "name");
await om.WriteSchemaSnapshotAsync(2, "base test schema");
await om.DefineClassAsync("TransientType", "Should be removed by rollback");
await om.RollbackSchemaAsync(2, strict: true);
var migration = await om.ApplySchemaMigrationAsync(new SchemaMigrationSpec(
    "mig:add-project",
    FromVersion: 2,
    ToVersion: 3,
    Label: "project schema",
    Steps:
    [
        JsonSerializer.SerializeToElement(new { kind = "addClass", className = "Project", description = "Project" }),
        JsonSerializer.SerializeToElement(new { kind = "addField", className = "Project", fieldName = "code", valueKind = "String", required = true }),
        JsonSerializer.SerializeToElement(new { kind = "addRelation", relationName = "assigned_project", fromClass = "Person", toClass = "Project" })
    ]));
Assert(migration is { FromVersion: 2, ToVersion: 3, StepsApplied: 3 }, "schema migration should report applied steps");
Assert((await om.GetSchemaStateAsync()).CurrentVersion == 3, "schema migration should advance current version");
await om.CreateObjectAsync("proj1", "Project", "Project 1");
await om.SetFieldValueAsync("proj1", "code", "P-001");

await ExpectCozoExceptionAsync(
    () => om.CreateObjectAsync("missing:create", "MissingType", "Missing"),
    "Class 'MissingType' does not exist",
    "createObject should reject unknown class names");
await ExpectCozoExceptionAsync(
    () => om.UpsertObjectAsync("missing:upsert", "MissingType", "Missing"),
    "Class 'MissingType' does not exist",
    "upsertObject should reject unknown class names");

await ExpectCozoExceptionAsync(
    () => om.DefineClassAsync("Orphan", "Orphan", parentClass: "NonExistent"),
    "Parent class 'NonExistent' does not exist",
    "defineClass should reject missing parent_class");
await om.DefineClassAsync("CycleA", "Cycle A");
await om.DefineClassAsync("CycleB", "Cycle B", parentClass: "CycleA");
await ExpectCozoExceptionAsync(
    () => om.DefineClassAsync("CycleA", "Cycle A", parentClass: "CycleB"),
    "Circular inheritance detected",
    "defineType should reject circular inheritance");
await om.DefineClassAsync("SelfCycle", "Self cycle");
await ExpectCozoExceptionAsync(
    () => om.DefineClassAsync("SelfCycle", "Self cycle", parentClass: "SelfCycle"),
    "Circular inheritance detected",
    "defineType should reject self-referencing parent");

var employeeAncestors = await om.GetAncestorsAsync("Employee");
Assert(employeeAncestors.SequenceEqual(["Person"]), "getAncestors should return nearest-to-farthest parent chain");
var personDescendants = await om.GetDescendantsAsync("Person");
Assert(personDescendants.Contains("Employee"), "getDescendants should include child types");
Assert(await om.IsSubclassOfAsync("Employee", "Person"), "isSubtypeOf should accept inherited type relation");
var hierarchy = await om.GetClassHierarchyAsync();
Assert(hierarchy.Roots.Contains("Person") && hierarchy.Classes["Employee"].ParentClass == "Person", "getClassHierarchy should expose roots and parent links");

await om.DefineClassAliasAsync("Staff", "Person");
await om.DefineRelationDefAliasAsync("member_of", "works_in");
Assert(await om.ResolveClassAsync("Staff") == "Person", "resolveClass should canonicalize class aliases");
Assert(await om.ResolveRelationAsync("member_of") == "works_in", "resolveRelation should canonicalize relation aliases");
Assert(await om.ResolveFieldAsync("Person", "full_name") == "name", "resolveField should canonicalize field aliases");
await om.CreateObjectAsync("alias-rel-person", "Employee", "Alias Relation Person");
await om.CreateObjectAsync("alias-rel-dept", "Department", "Alias Relation Department");
await om.CreateRelationLinkAsync("alias-rel-person", "member_of", "alias-rel-dept");
Assert((await om.GetNeighborsAsync("alias-rel-person", "works_in", OmDirection.Outgoing)).Outgoing.Any(n => n.ObjectId == "alias-rel-dept"), "relation alias should work for link writes");

await om.DefineClassAsync("AliasCycleObject", "Alias cycle object");
await om.DefineFieldAliasAsync("AliasCycleObject", "a", "b");
await om.DefineFieldAliasAsync("AliasCycleObject", "b", "a");
await ExpectCozoExceptionAsync(
    () => om.ResolveFieldAsync("AliasCycleObject", "a"),
    "cycle",
    "resolveField should reject alias cycles");

await om.DefineClassAsync("AliasCanonicalClass", "Alias canonical class");
await om.DefineFieldAsync("AliasCanonicalClass", "org_unit", OmValueType.String);
await om.DefineClassAliasAsync("AliasStoredClass", "AliasCanonicalClass");
db.Run(
    """
    ?[id, class_name, label] <- [[$id, $class_name, $label]]
    :insert om_object {id => class_name, label}
    """,
    new { id = "alias:class-view", class_name = "AliasStoredClass", label = "Alias Class View" });
await om.SetFieldValueAsync("alias:class-view", "org_unit", "PeopleOps");
var aliasClassView = await om.GetObjectViewAsync("alias:class-view");
Assert(aliasClassView is not null && aliasClassView.ClassName == "AliasCanonicalClass", "getObjectView should return canonical className for alias-stored objects");

await om.DefineMixinAsync("Auditable", "Auditable mixin");
await om.DefineFieldAsync("Auditable", "created_by", OmValueType.String);
await om.DefineClassAsync("AuditedAsset", "Audited asset", mixins: ["Auditable"]);
var auditedDefs = await om.GetFieldDefinitionsAsync("AuditedAsset");
Assert(auditedDefs.TryGetValue("created_by", out var createdByDef) && createdByDef.ValueKind == OmValueType.String, "mixin field should be part of effective definitions");
await om.DefineClassAsync("AuditedAsset", "Audited asset updated");
var auditedDefsAfterRedefine = await om.GetFieldDefinitionsAsync("AuditedAsset");
Assert(auditedDefsAfterRedefine.ContainsKey("created_by"), "redefining a class without options should preserve mixins");
await om.CreateObjectAsync("audited:1", "AuditedAsset", "Audited 1");
await om.SetFieldValueAsync("audited:1", "created_by", "admin");
Assert(AsString(await om.GetFieldValueAsync("audited:1", "created_by")) == "admin", "mixin-contributed field should be writable");

await om.DefineClassAsync("DescBase", "Description base");
await om.DefineFieldAsync("DescBase", "status", OmValueType.String, description: "Base status");
await om.DefineClassAsync("DescChild", "Description child", parentClass: "DescBase");
await om.DefineFieldAsync("DescChild", "status", OmValueType.String, description: "Child status");
await om.DefineClassAsync("DescGrandchild", "Description grandchild", parentClass: "DescChild");
Assert((await om.GetFieldDefinitionsAsync("DescBase"))["status"].Description == "Base status", "field descriptions should be stored");
Assert((await om.GetFieldDefinitionsAsync("DescGrandchild"))["status"].Description == "Child status", "nearest inherited field description should win");

await om.DefineClassAsync("OverrideBase", "Override base");
await om.DefineFieldAsync("OverrideBase", "name", OmValueType.String, required: true);
await om.DefineFieldAsync("OverrideBase", "location", OmValueType.String);
await om.DefineClassAsync("OverrideChild", "Override child", parentClass: "OverrideBase");
await om.DefineFieldAsync("OverrideChild", "location", OmValueType.String, required: true);
var overrideDefs = await om.GetFieldDefinitionsAsync("OverrideChild");
Assert(overrideDefs["location"].Required, "child class should be able to tighten optional inherited field to required");
var loosenRejected = false;
try
{
    await om.DefineFieldAsync("OverrideChild", "name", OmValueType.String, required: false);
}
catch (CozoException ex) when (ex.Message.Contains("Cannot loosen required", StringComparison.Ordinal))
{
    loosenRejected = true;
}

Assert(loosenRejected, "child class should not loosen inherited required field");
var typeChangeRejected = false;
try
{
    await om.DefineFieldAsync("OverrideChild", "name", OmValueType.Number, required: true);
}
catch (CozoException ex) when (ex.Message.Contains("Cannot change value_kind", StringComparison.Ordinal))
{
    typeChangeRejected = true;
}

Assert(typeChangeRejected, "child class should not change inherited field value kind");

await om.DefineClassAsync("PreserveParent", "Preserve parent");
await om.DefineClassAsync("PreserveChild", "Preserve child", parentClass: "PreserveParent");
await om.DefineClassAsync("PreserveChild", "Preserve child updated");
Assert(await om.IsSubclassOfAsync("PreserveChild", "PreserveParent"), "redefining an existing class without parent should preserve parent_class");

await om.DefineClassAsync("AggAsset", "Aggregate asset");
await om.DefineClassAsync("AggServer", "Aggregate server", parentClass: "AggAsset");
await om.DefineFieldAsync("AggAsset", "value", OmValueType.Number);
await om.CreateObjectAsync("agg:asset", "AggAsset", "Agg Asset");
await om.SetFieldValueAsync("agg:asset", "value", 10);
await om.CreateObjectAsync("agg:server", "AggServer", "Agg Server");
await om.SetFieldValueAsync("agg:server", "value", 20);
Assert(await om.AggregateByClassAsync("AggAsset", "value", "sum") == 30, "aggregateByClass should include descendants by default");
Assert(await om.AggregateByClassAsync("AggAsset", "value", "avg") == 15, "aggregateByClass should support avg");
Assert(await om.AggregateByClassAsync("AggAsset", "value", "min") == 10, "aggregateByClass should support min");
Assert(await om.AggregateByClassAsync("AggAsset", "value", "max") == 20, "aggregateByClass should support max");
Assert(await om.AggregateByClassAsync("AggAsset", "value", "count") == 2, "aggregateByClass should support count");
Assert(await om.AggregateByClassAsync("AggAsset", "value", "sum", new FindByClassOptions(Exact: true)) == 10, "aggregateByClass exact mode should only sum exact class rows");
Assert(await om.AggregateByClassAsync("AggAsset", "value", "count", new FindByClassOptions(Exact: true)) == 1, "aggregateByClass exact mode should only count exact class rows");

await om.DefineClassAsync("AliasFallbackEntity", "Alias fallback entity");
await om.DefineFieldAsync("AliasFallbackEntity", "canonical_name", OmValueType.String, required: true);
await om.DefineFieldAliasAsync("AliasFallbackEntity", "legacy_name", "canonical_name");
await om.CreateObjectAsync("alias:fallback", "AliasFallbackEntity", "Alias fallback");
db.Run(
    """
    ?[object_id, field_name, valid_time, value, tx_time] <- [[$object_id, $field_name, "2000-01-01T00:00:00Z", $value, "2000-01-01T00:00:00Z"]]
    :put om_field_value {object_id, field_name, valid_time => value, tx_time}
    """,
    new { object_id = "alias:fallback", field_name = "legacy_name", value = "Legacy Value" });
Assert(AsString(await om.GetFieldValueAsync("alias:fallback", "canonical_name")) == "Legacy Value", "canonical field read should fall back to legacy alias-stored row");
await om.SetFieldValueAsync("alias:fallback", "canonical_name", "Canonical Value");
Assert(AsString(await om.GetFieldValueAsync("alias:fallback", "canonical_name")) == "Canonical Value", "canonical field row should win when both canonical and alias rows exist");
Assert((await om.ValidateObjectAsync("alias:fallback")).Valid, "required validation should canonicalize alias-stored field values");

await om.DefineClassAsync("ValidityHolder", "Validity holder");
await om.DefineFieldAsync("ValidityHolder", "valid_from", OmValueType.Validity);
await om.CreateObjectAsync("validity:1", "ValidityHolder", "Validity 1");
var validityIso = "2026-01-01T00:00:00Z";
await om.SetFieldValueAsync("validity:1", "valid_from", validityIso);
using var validityRows = db.Run(
    """
    ?[ts] :=
      *om_field_value{ object_id: $object_id, field_name: $field_name, value: v },
      ts = to_int(v)
    """,
    new { object_id = "validity:1", field_name = "valid_from" });
Assert(Rows(validityRows)[0][0].GetInt64() == DateTimeOffset.Parse(validityIso).ToUnixTimeMilliseconds() * 1000, "Validity field should be stored as Cozo validity value");

await om.CreateObjectAsync("p1", "Employee", "Alice");
await om.CreateObjectAsync("d1", "Department", "Engineering");
await om.CreateObjectAsync("undir:source", "UndirectedSource", "Undirected Source");
await om.CreateObjectAsync("undir:target", "UndirectedTarget", "Undirected Target");
await om.CreateRelationLinkAsync("undir:target", "paired_with", "undir:source");
Assert((await om.GetNeighborsAsync("undir:target", "paired_with", OmDirection.Outgoing)).Outgoing.Count == 1, "undirected relation should allow reverse endpoint typing");
var invalidBeforeName = await om.ValidateObjectAsync("p1");
Assert(!invalidBeforeName.Valid && invalidBeforeName.Errors.Any(e => e.Contains("name", StringComparison.Ordinal)), "required field value should be enforced");

await om.SetFieldValueAsync("p1", "full_name", "Alice");
await om.SetFieldValueAsync("p1", "age", 42);
Assert(AsString(await om.GetFieldValueAsync("p1", "name")) == "Alice", "field value read should return current value");
await om.SetFieldValueAsync("p1", "name", "Alice 2024", new WriteOptions(ValidTime: "2024-01-01T00:00:00Z"));
await om.SetFieldValueAsync("p1", "name", "Alice 2025", new WriteOptions(ValidTime: "2025-01-01T00:00:00Z"));
Assert(AsString(await om.GetFieldValueAsOfAsync("p1", "name", "2024-06-01T00:00:00Z")) == "Alice 2024", "field value asOf should read historical value");

await om.CreateRelationLinkAsync("p1", "works_in", "d1");
var neighbors = await om.GetNeighborsAsync("p1", "works_in", OmDirection.Outgoing);
Assert(neighbors.Outgoing.Count == 1 && neighbors.Outgoing[0].ObjectId == "d1", "outgoing relation should be visible");
await om.CreateObjectAsync("p_temporal", "Person", "Temporal");
await om.SetFieldValueAsync("p_temporal", "name", "Temporal");
await om.CreateObjectAsync("d_temporal", "Department", "Temporal Dept");
await om.CreateRelationLinkAsync("p_temporal", "works_in", "d_temporal", options: new WriteOptions(ValidTime: "2024-01-01T00:00:00Z"));
Assert((await om.GetNeighborsAsOfAsync("p_temporal", "works_in", "2024-06-01T00:00:00Z", OmDirection.Outgoing)).Outgoing.Count == 1, "relation link asOf should see asserted relation link");
await om.RetractRelationLinkAsync("p_temporal", "works_in", "d_temporal", new WriteOptions(ValidTime: "2025-01-01T00:00:00Z"));
Assert((await om.GetNeighborsAsOfAsync("p_temporal", "works_in", "2025-06-01T00:00:00Z", OmDirection.Outgoing)).Outgoing.Count == 0, "relation link asOf should honor retract");

await om.DefineClassAsync("ViewEmployee", "View employee");
await om.DefineClassAsync("ViewDepartment", "View department");
await om.DefineFieldAsync("ViewEmployee", "department", OmValueType.String);
await om.DefineFieldAsync("ViewEmployee", "base_score", OmValueType.Number);
await om.DefineRelationDefAsync("view_works_in", "ViewEmployee", "ViewDepartment");
await om.CreateObjectAsync("view:emp", "ViewEmployee", "View Emp");
await om.CreateObjectAsync("view:dept", "ViewDepartment", "View Dept");
await om.SetFieldValueAsync("view:emp", "department", "Engineering", new WriteOptions(ValidTime: "2024-01-01T00:00:00Z"));
await om.SetFieldValueAsync("view:emp", "department", "Product", new WriteOptions(ValidTime: "2025-01-01T00:00:00Z"));
await om.SetFieldValueAsync("view:emp", "base_score", 7, new WriteOptions(ValidTime: "2024-01-01T00:00:00Z"));
await om.CreateRelationLinkAsync("view:emp", "view_works_in", "view:dept", options: new WriteOptions(ValidTime: "2024-01-01T00:00:00Z"));
await om.RetractRelationLinkAsync("view:emp", "view_works_in", "view:dept", new WriteOptions(ValidTime: "2025-01-01T00:00:00Z"));
await om.DefineComputedPropAsync("ViewEmployee", "dept_code");
om.RegisterComputedProp("ViewEmployee", "dept_code", async ctx =>
{
    var department = AsString(await ctx.GetFieldValueAsync("department"));
    return department is null ? null : department[..Math.Min(3, department.Length)].ToUpperInvariant();
});
var viewAsOf = await om.GetObjectViewAsOfAsync("view:emp", "2024-06-01T00:00:00Z");
Assert(viewAsOf is not null
       && AsString(viewAsOf.FieldValues["department"]) == "Engineering"
       && AsString(viewAsOf.FieldValues["dept_code"]) == "ENG"
       && viewAsOf.Outgoing.Any(link => link.ToObjectId == "view:dept"),
    "GetObjectViewAsOfAsync should include historical properties, computedProp values, and outgoing relation links");
var viewNow = await om.GetObjectViewAsync("view:emp");
Assert(viewNow is not null
       && AsString(viewNow.FieldValues["department"]) == "Product"
       && viewNow.Outgoing.Count == 0,
    "GetObjectViewAsync should keep NOW semantics after adding asOf view");

await om.DefineClassAsync("SearchAsset", "Search asset");
await om.DefineClassAsync("SearchServer", "Search server", parentClass: "SearchAsset");
await om.DefineFieldAsync("SearchAsset", "status", OmValueType.String);
await om.DefineFieldAsync("SearchAsset", "tier", OmValueType.String);
await om.CreateObjectAsync("search:asset", "SearchAsset", "Search Asset");
await om.CreateObjectAsync("search:server", "SearchServer", "Search Server");
await om.SetFieldValueAsync("search:asset", "status", "active");
await om.SetFieldValueAsync("search:asset", "tier", "gold");
await om.SetFieldValueAsync("search:server", "status", "active");
await om.SetFieldValueAsync("search:server", "tier", "silver");
var richSearch = await om.FindByClassWithFieldValuesAsync("SearchAsset", new Dictionary<string, object?> { ["status"] = "active" });
Assert(richSearch.Count == 2
       && richSearch.Any(entry => entry.Id == "search:server" && AsString(entry.FieldValues["tier"]) == "silver"),
    "FindByClassWithFieldValuesAsync should filter by property and return canonical properties");
var exactRichSearch = await om.FindByClassWithFieldValuesAsync("SearchAsset", new Dictionary<string, object?> { ["status"] = "active" }, new FindByClassOptions(Exact: true));
Assert(exactRichSearch.Count == 1 && exactRichSearch[0].Id == "search:asset", "rich type search exact mode should exclude descendants");

await om.DefineClassAsync("ValidationPerson", "Validation person");
await om.DefineClassAsync("ValidationDepartment", "Validation department");
await om.DefineFieldAsync("ValidationPerson", "name", OmValueType.String, required: true);
await om.DefineRelationDefAsync("validation_member_of", "ValidationPerson", "ValidationDepartment");
await om.CreateObjectAsync("validation:person", "ValidationPerson", "Validation Person");
await om.CreateObjectAsync("validation:dept", "ValidationDepartment", "Validation Dept");
var missingRequired = await om.ValidateRequiredFieldValuesAsync("validation:person");
Assert(missingRequired.SequenceEqual(["name"]), "ValidateRequiredFieldValuesAsync should return missing canonical attribute names");
await ExpectCozoExceptionAsync(
    () => om.ValidateFieldValueTypeAsync("validation:person", "name", 123),
    "expects String",
    "ValidateFieldValueTypeAsync should reject mismatched value type without writing");
await ExpectCozoExceptionAsync(
    () => om.ValidateRelationAsync("validation:dept", "validation_member_of", "validation:person"),
    "cannot link",
    "ValidateRelationAsync should reject invalid endpoint typing without writing");
await ExpectCozoExceptionAsync(
    () => om.FinalizeObjectAsync("validation:person"),
    "Missing required property",
    "FinalizeObjectAsync should fail incomplete entities");
await om.SetFieldValueAsync("validation:person", "name", "Valid");
await om.FinalizeObjectAsync("validation:person");

await om.DefineClassAsync("BehaviorGuardOwner", "Behavior guard owner");
var alwaysTrue = new Func<OmValidationContext, ValueTask<bool>>(_ => ValueTask.FromResult(true));
const string missingBehaviorOwner = "MissingBehaviorOwner";

await ExpectCozoExceptionAsync(
    () => om.DefineConstraintAsync(missingBehaviorOwner, "missing_constraint", "conditional", alwaysTrue, alwaysTrue),
    "does not exist",
    "constraint definitions should reject a missing owner class");
await ExpectCozoExceptionAsync(
    () => om.DefineComputedPropAsync(missingBehaviorOwner, "missing_computed_prop"),
    "does not exist",
    "computedProp prop definitions should reject a missing owner class");
await ExpectCozoExceptionAsync(
    () => om.DefineOperationAsync(missingBehaviorOwner, "missing_operation", (_, _) => ValueTask.FromResult<IReadOnlyList<MutationSpec>>([])),
    "does not exist",
    "operation definitions should reject a missing owner class");
await ExpectCozoExceptionAsync(
    () => om.DefineMutationAsync(missingBehaviorOwner, "missing_mutation", (_, _) => ValueTask.CompletedTask),
    "does not exist",
    "mutation definitions should reject a missing owner class");
await ExpectCozoExceptionAsync(
    () => om.AddInterceptorAsync(missingBehaviorOwner, "missing_interceptor", "before", _ => ValueTask.CompletedTask),
    "does not exist",
    "interceptor definitions should reject a missing owner class");
Assert(DefinitionCount(db, "constraint", missingBehaviorOwner, "missing_constraint") == 0
       && DefinitionCount(db, "computedProp", missingBehaviorOwner, "missing_computed_prop") == 0
       && DefinitionCount(db, "operation", missingBehaviorOwner, "missing_operation") == 0
       && DefinitionCount(db, "mutation", missingBehaviorOwner, "missing_mutation") == 0
       && DefinitionCount(db, "interceptor", missingBehaviorOwner, "missing_interceptor") == 0,
    "missing behavior owners should leave no persistent metadata");
Assert(!om.Runtime.Registry.TryGetConstraint(missingBehaviorOwner, "missing_constraint", out _)
       && !om.Runtime.Registry.TryGetOperation(missingBehaviorOwner, "missing_operation", out _)
       && !om.Runtime.Registry.TryGetMutation(missingBehaviorOwner, "missing_mutation", out _)
       && om.Runtime.Registry.GetInterceptors(missingBehaviorOwner, "missing_interceptor", "before").Count == 0,
    "missing behavior owners should leave no callback registrations");

await ExpectExceptionAsync<ArgumentException>(
    () => om.DefineConstraintAsync("BehaviorGuardOwner", "invalid_scope", "request", alwaysTrue, alwaysTrue),
    "constraint definitions should reject unknown scopes");
Assert(DefinitionCount(db, "constraint", "BehaviorGuardOwner", "invalid_scope") == 0
       && !om.Runtime.Registry.TryGetConstraint("BehaviorGuardOwner", "invalid_scope", out _),
    "invalid constraint scopes should leave no metadata or callback");

await ExpectExceptionAsync<ArgumentException>(
    () => om.AddInterceptorAsync("BehaviorGuardOwner", "invalid_phase", "around", _ => ValueTask.CompletedTask),
    "interceptor definitions should reject unknown phases");
Assert(DefinitionCount(db, "interceptor", "BehaviorGuardOwner", "invalid_phase") == 0
       && om.Runtime.Registry.GetInterceptors("BehaviorGuardOwner", "invalid_phase", "before").Count == 0
       && om.Runtime.Registry.GetInterceptors("BehaviorGuardOwner", "invalid_phase", "after").Count == 0,
    "invalid interceptor phases should not fall back to a before registration");
await ExpectExceptionAsync<ArgumentException>(
    () => Task.Run(() => om.Runtime.Registry.RegisterInterceptor(
        "BehaviorGuardOwner",
        "invalid_registry_phase",
        "around",
        _ => ValueTask.CompletedTask)),
    "the public callback registry should reject unknown interceptor phases");
Assert(om.Runtime.Registry.GetInterceptors("BehaviorGuardOwner", "invalid_registry_phase", "before").Count == 0,
    "an invalid public registry phase should not create a before interceptor fallback");

await ExpectExceptionAsync<ArgumentNullException>(
    () => om.DefineConstraintAsync(
        "BehaviorGuardOwner",
        "null_constraint_when",
        "conditional",
        (Func<OmValidationContext, ValueTask<bool>>)null!,
        alwaysTrue),
    "constraint definitions should reject a null when callback");
await ExpectExceptionAsync<ArgumentNullException>(
    () => om.DefineConstraintAsync(
        "BehaviorGuardOwner",
        "null_constraint_then",
        "conditional",
        alwaysTrue,
        (Func<OmValidationContext, ValueTask<bool>>)null!),
    "constraint definitions should reject a null then callback");
await ExpectExceptionAsync<ArgumentNullException>(
    () => om.DefineOperationAsync(
        "BehaviorGuardOwner",
        "null_operation",
        (Func<OmOperationContext, IReadOnlyDictionary<string, object?>, ValueTask<IReadOnlyList<MutationSpec>>>)null!),
    "operation definitions should reject a null callback");
await ExpectExceptionAsync<ArgumentNullException>(
    () => om.DefineMutationAsync(
        "BehaviorGuardOwner",
        "null_mutation",
        (Func<OmMutationContext, IReadOnlyDictionary<string, object?>, ValueTask>)null!),
    "mutation definitions should reject a null callback");
await ExpectExceptionAsync<ArgumentNullException>(
    () => om.AddInterceptorAsync(
        "BehaviorGuardOwner",
        "null_interceptor",
        "before",
        (Func<OmOperationContext, ValueTask>)null!),
    "interceptor definitions should reject a null callback");
Assert(DefinitionCount(db, "constraint", "BehaviorGuardOwner", "null_constraint_when") == 0
       && DefinitionCount(db, "constraint", "BehaviorGuardOwner", "null_constraint_then") == 0
       && DefinitionCount(db, "operation", "BehaviorGuardOwner", "null_operation") == 0
       && DefinitionCount(db, "mutation", "BehaviorGuardOwner", "null_mutation") == 0
       && DefinitionCount(db, "interceptor", "BehaviorGuardOwner", "null_interceptor") == 0,
    "null callbacks should be rejected before persistent metadata is written");

HarnessDiagnostics.Start("behavior registration failure matrix");

using (var failingDb = new CozoDb(engine: "mem", path: ""))
{
    var failingStore = new ScriptFailingOmStore(new CozoDbOmStore(failingDb));
    var failingOm = new CozoOm(failingStore);
    await failingOm.InitSchemaAsync();
    await failingOm.DefineClassAsync("FailingBehaviorOwner", "Failing behavior owner");
    failingStore.FailWhenScriptContains = ":put om_interceptor_def";
    await ExpectExceptionAsync<InvalidOperationException>(
        () => failingOm.AddInterceptorAsync("FailingBehaviorOwner", "storage_failure", "before", _ => ValueTask.CompletedTask),
        "interceptor definition should surface persistent metadata failures");
    Assert(failingOm.Runtime.Registry.GetInterceptors("FailingBehaviorOwner", "storage_failure", "before").Count == 0
           && DefinitionCount(failingDb, "interceptor", "FailingBehaviorOwner", "storage_failure") == 0,
        "persistent interceptor failure should leave no metadata or callback ghost");
}

await ExpectExceptionAsync<InvalidOperationException>(
    () => ConstraintLogic.AddInterceptorCallbackAsync(
        om.Runtime,
        new AddInterceptorInput("BehaviorGuardOwner", "registry_failure", "before", 0, "registry failure"),
        _ => ValueTask.CompletedTask,
        afterRegistration: () => throw new InvalidOperationException("simulated registry failure")),
    "interceptor definition should surface callback registration failures");
Assert(DefinitionCount(db, "interceptor", "BehaviorGuardOwner", "registry_failure") == 0
       && om.Runtime.Registry.GetInterceptors("BehaviorGuardOwner", "registry_failure", "before").Count == 0,
    "callback registration failure should remove newly written metadata and callback state");

var oldConstraintWhen = new Func<OmValidationContext, ValueTask<bool>>(_ => ValueTask.FromResult(false));
var oldConstraintThen = new Func<OmValidationContext, ValueTask<bool>>(_ => ValueTask.FromResult(true));
const string oldConstraintWhenBindingId = "binding:overwrite:constraint:when";
const string oldConstraintThenBindingId = "binding:overwrite:constraint:then";
using (db.Run(
           """
           ?[class_name, constraint_name, constraint_kind, message] <-
             [[$class_name, $constraint_name, $constraint_kind, $message]]
           :put om_constraint_def {class_name, constraint_name => constraint_kind, message}
           """,
           new
           {
               class_name = "BehaviorGuardOwner",
               constraint_name = "overwrite_constraint",
               constraint_kind = "legacy-unsupported-scope",
               message = "old constraint message",
           }))
{
}
om.Runtime.Registry.RegisterConstraint(
    "BehaviorGuardOwner",
    "overwrite_constraint",
    oldConstraintWhenBindingId,
    oldConstraintWhen,
    oldConstraintThenBindingId,
    oldConstraintThen);
await BehaviorBindingLogic.PutAsync(
    om.Runtime,
    new BehaviorBindingRow(
        new BehaviorBindingKey(
            BehaviorKind.Constraint,
            "BehaviorGuardOwner",
            "overwrite_constraint",
            BehaviorCallbackSlot.When,
            BehaviorBindingLogic.NonInterceptorPhase,
            BehaviorBindingLogic.NonInterceptorSeq),
        oldConstraintWhenBindingId));
await BehaviorBindingLogic.PutAsync(
    om.Runtime,
    new BehaviorBindingRow(
        new BehaviorBindingKey(
            BehaviorKind.Constraint,
            "BehaviorGuardOwner",
            "overwrite_constraint",
            BehaviorCallbackSlot.Then,
            BehaviorBindingLogic.NonInterceptorPhase,
            BehaviorBindingLogic.NonInterceptorSeq),
        oldConstraintThenBindingId));
Assert((await om.GetBehaviorCatalogAsync()).Behaviors
        .Single(entry => entry.Kind == BehaviorCatalogKind.Constraint && entry.Name == "overwrite_constraint")
        .Callbacks.All(callback => callback.Readiness == BehaviorReadiness.Ready),
    "constraint overwrite fixture should begin with both portable callbacks ready");
await ExpectExceptionAsync<InvalidOperationException>(
    () => ConstraintLogic.DefineConstraintCallbackAsync(
        om.Runtime,
        new DefineConstraintInput("BehaviorGuardOwner", "overwrite_constraint", "custom", "new constraint message"),
        _ => ValueTask.FromResult(true),
        _ => ValueTask.FromResult(false),
        afterRegistration: () => throw new InvalidOperationException("simulated constraint registration failure")),
    "constraint overwrite should surface callback registration failures");
Assert(DefinitionPayload(db, "constraint", "BehaviorGuardOwner", "overwrite_constraint") ==
       "legacy-unsupported-scope|old constraint message",
    "constraint overwrite failure should raw-restore the complete legacy metadata payload");
Assert(om.Runtime.Registry.TryGetConstraint("BehaviorGuardOwner", "overwrite_constraint", out var restoredConstraint)
       && ReferenceEquals(restoredConstraint.When, oldConstraintWhen)
       && ReferenceEquals(restoredConstraint.Then, oldConstraintThen)
       && restoredConstraint.WhenBindingId == oldConstraintWhenBindingId
       && restoredConstraint.ThenBindingId == oldConstraintThenBindingId,
    "constraint overwrite failure should restore both previous callbacks and binding identities");
Assert((await om.GetBehaviorCatalogAsync()).Behaviors
        .Single(entry => entry.Kind == BehaviorCatalogKind.Constraint && entry.Name == "overwrite_constraint")
        .Callbacks.All(callback => callback.Readiness == BehaviorReadiness.Ready),
    "constraint overwrite compensation should keep both portable callbacks ready");

var oldOperation = new Func<OmOperationContext, IReadOnlyDictionary<string, object?>, ValueTask<IReadOnlyList<MutationSpec>>>(
    (_, _) => ValueTask.FromResult<IReadOnlyList<MutationSpec>>([]));
const string oldActionBindingId = "binding:overwrite:operation";
await om.DefineOperationAsync("BehaviorGuardOwner", "overwrite_operation", "old operation description");
om.Runtime.Registry.RegisterOperation("BehaviorGuardOwner", "overwrite_operation", oldActionBindingId, oldOperation);
await BehaviorBindingLogic.PutAsync(
    om.Runtime,
    new BehaviorBindingRow(
        new BehaviorBindingKey(
            BehaviorKind.Operation,
            "BehaviorGuardOwner",
            "overwrite_operation",
            BehaviorCallbackSlot.Handler,
            BehaviorBindingLogic.NonInterceptorPhase,
            BehaviorBindingLogic.NonInterceptorSeq),
        oldActionBindingId));
Assert((await om.GetBehaviorCatalogAsync()).Behaviors
        .Single(entry => entry.Kind == BehaviorCatalogKind.Operation && entry.Name == "overwrite_operation")
        .Callbacks.Single().Readiness == BehaviorReadiness.Ready,
    "operation overwrite fixture should begin with its portable callback ready");
await ExpectExceptionAsync<InvalidOperationException>(
    () => ConstraintLogic.DefineOperationCallbackAsync(
        om.Runtime,
        new DefineOperationInput("BehaviorGuardOwner", "overwrite_operation", "new operation description"),
        (_, _) => ValueTask.FromResult<IReadOnlyList<MutationSpec>>([]),
        afterRegistration: () => throw new InvalidOperationException("simulated operation registration failure")),
    "operation overwrite should surface callback registration failures");
Assert(DefinitionPayload(db, "operation", "BehaviorGuardOwner", "overwrite_operation") == "old operation description",
    "operation overwrite failure should restore the previous description");
Assert(om.Runtime.Registry.TryGetOperation("BehaviorGuardOwner", "overwrite_operation", out var restoredOperation)
       && ReferenceEquals(restoredOperation, oldOperation),
    "operation overwrite failure should restore the previous callback");
Assert((await om.GetBehaviorCatalogAsync()).Behaviors
        .Single(entry => entry.Kind == BehaviorCatalogKind.Operation && entry.Name == "overwrite_operation")
        .Callbacks.Single().Readiness == BehaviorReadiness.Ready,
    "operation overwrite compensation should preserve the exact binding identity and readiness");

var oldMutation = new Func<OmMutationContext, IReadOnlyDictionary<string, object?>, ValueTask>(
    (_, _) => ValueTask.CompletedTask);
const string oldMutationBindingId = "binding:overwrite:mutation";
await om.DefineMutationAsync("BehaviorGuardOwner", "overwrite_mutation", "old mutation description");
om.Runtime.Registry.RegisterMutation("BehaviorGuardOwner", "overwrite_mutation", oldMutationBindingId, oldMutation);
await BehaviorBindingLogic.PutAsync(
    om.Runtime,
    new BehaviorBindingRow(
        new BehaviorBindingKey(
            BehaviorKind.Mutation,
            "BehaviorGuardOwner",
            "overwrite_mutation",
            BehaviorCallbackSlot.Executor,
            BehaviorBindingLogic.NonInterceptorPhase,
            BehaviorBindingLogic.NonInterceptorSeq),
        oldMutationBindingId));
Assert((await om.GetBehaviorCatalogAsync()).Behaviors
        .Single(entry => entry.Kind == BehaviorCatalogKind.Mutation && entry.Name == "overwrite_mutation")
        .Callbacks.Single().Readiness == BehaviorReadiness.Ready,
    "mutation overwrite fixture should begin with its portable callback ready");
await ExpectExceptionAsync<InvalidOperationException>(
    () => ConstraintLogic.DefineMutationCallbackAsync(
        om.Runtime,
        new DefineMutationInput("BehaviorGuardOwner", "overwrite_mutation", "new mutation description"),
        (_, _) => ValueTask.CompletedTask,
        afterRegistration: () => throw new InvalidOperationException("simulated mutation registration failure")),
    "mutation overwrite should surface callback registration failures");
Assert(DefinitionPayload(db, "mutation", "BehaviorGuardOwner", "overwrite_mutation") == "old mutation description",
    "mutation overwrite failure should restore the previous description");
Assert(om.Runtime.Registry.TryGetMutation("BehaviorGuardOwner", "overwrite_mutation", out var restoredMutation)
       && ReferenceEquals(restoredMutation, oldMutation),
    "mutation overwrite failure should restore the previous callback");
Assert((await om.GetBehaviorCatalogAsync()).Behaviors
        .Single(entry => entry.Kind == BehaviorCatalogKind.Mutation && entry.Name == "overwrite_mutation")
        .Callbacks.Single().Readiness == BehaviorReadiness.Ready,
    "mutation overwrite compensation should preserve the exact binding identity and readiness");

var oldInterceptor = new Func<OmOperationContext, ValueTask>(_ => ValueTask.CompletedTask);
const string oldInterceptorBindingId = "binding:overwrite:interceptor:before:0";
await om.AddInterceptorAsync(
    "BehaviorGuardOwner",
    "overwrite_interceptor",
    "before",
    seq: 0,
    description: "old interceptor description");
om.Runtime.Registry.RegisterInterceptor(
    "BehaviorGuardOwner",
    "overwrite_interceptor",
    "before",
    0,
    oldInterceptorBindingId,
    oldInterceptor,
    "old interceptor description");
await BehaviorBindingLogic.PutAsync(
    om.Runtime,
    new BehaviorBindingRow(
        new BehaviorBindingKey(
            BehaviorKind.Interceptor,
            "BehaviorGuardOwner",
            "overwrite_interceptor",
            BehaviorCallbackSlot.Handler,
            "before",
            0),
        oldInterceptorBindingId));
Assert((await om.GetBehaviorCatalogAsync()).Behaviors
        .Single(entry => entry.Kind == BehaviorCatalogKind.Interceptor && entry.Name == "overwrite_interceptor")
        .Callbacks.Single().Readiness == BehaviorReadiness.Ready,
    "interceptor overwrite fixture should begin with its exact portable callback ready");

om.Runtime.Registry.UnregisterInterceptor("BehaviorGuardOwner", "overwrite_interceptor", "before", 0);
var interceptorHookStore = new RunHookOmStore(new CozoDbOmStore(db))
{
    RunOnceWhenScriptContains = "*om_interceptor_def",
    OnRunOnce = () => om.Runtime.Registry.RegisterInterceptor(
        "BehaviorGuardOwner",
        "overwrite_interceptor",
        "before",
        0,
        oldInterceptorBindingId,
        oldInterceptor,
        "old interceptor description"),
};
var interceptorHookRuntime = new CozoOmRuntime(interceptorHookStore, om.Runtime.Options, om.Runtime.Registry);
await ExpectExceptionAsync<InvalidOperationException>(
    () => ConstraintLogic.AddInterceptorCallbackAsync(
        interceptorHookRuntime,
        new AddInterceptorInput("BehaviorGuardOwner", "overwrite_interceptor", "before", 0, "new interceptor description"),
        _ => ValueTask.CompletedTask,
        afterRegistration: () => throw new InvalidOperationException("simulated interceptor registration failure")),
    "interceptor overwrite should surface callback registration failures");
Assert(DefinitionPayload(
           db,
           "interceptor",
           "BehaviorGuardOwner",
           "overwrite_interceptor",
           phase: "before",
           seq: 0) == "old interceptor description",
    "interceptor overwrite failure should restore metadata at the exact owner/operation/phase/seq key");
Assert(om.Runtime.Registry.TryGetInterceptor(
           "BehaviorGuardOwner",
           "overwrite_interceptor",
           "before",
           0,
           out var restoredInterceptor)
       && ReferenceEquals(restoredInterceptor.Handler, oldInterceptor)
       && restoredInterceptor.Description == "old interceptor description"
       && restoredInterceptor.OwnerClass == "BehaviorGuardOwner"
       && restoredInterceptor.Seq == 0
       && restoredInterceptor.BindingId == oldInterceptorBindingId,
    "interceptor overwrite failure should restore the previous callback and exact binding identity");
Assert((await om.GetBehaviorCatalogAsync()).Behaviors
        .Single(entry => entry.Kind == BehaviorCatalogKind.Interceptor && entry.Name == "overwrite_interceptor")
        .Callbacks.Single().Readiness == BehaviorReadiness.Ready,
    "interceptor overwrite compensation should preserve exact phase/seq binding readiness");

HarnessDiagnostics.Start("behavior compensation rollback matrix");

using (var rollbackFailingDb = new CozoDb(engine: "mem", path: ""))
{
    var rollbackFailingStore = new ScriptFailingOmStore(new CozoDbOmStore(rollbackFailingDb));
    var rollbackFailingOm = new CozoOm(rollbackFailingStore);
    await rollbackFailingOm.InitSchemaAsync();
    await rollbackFailingOm.DefineClassAsync("RollbackFailureOwner", "Rollback failure owner");
    await rollbackFailingOm.DefineOperationAsync(
        "RollbackFailureOwner",
        "rollback_failure",
        (_, _) => ValueTask.FromResult<IReadOnlyList<MutationSpec>>([]),
        "old rollback description");

    AggregateException? combinedFailure = null;
    try
    {
        await ConstraintLogic.DefineOperationCallbackAsync(
            rollbackFailingOm.Runtime,
            new DefineOperationInput("RollbackFailureOwner", "rollback_failure", "new rollback description"),
            (_, _) => ValueTask.FromResult<IReadOnlyList<MutationSpec>>([]),
            afterRegistration: () =>
            {
                rollbackFailingStore.FailWhenScriptContains = ":put om_operation_def";
                throw new InvalidOperationException("simulated operation registration failure");
            });
    }
    catch (AggregateException ex)
    {
        combinedFailure = ex;
    }

    Assert(combinedFailure is not null
           && combinedFailure.InnerExceptions.Any(ex => ex.Message.Contains("registration failure", StringComparison.Ordinal))
           && combinedFailure.InnerExceptions.Any(ex => ex.Message.Contains("persistent metadata failure", StringComparison.Ordinal)),
        "metadata rollback failure should report both the original registration and compensation failures");
}

await om.DefineClassAsync("ComputedBase", "ComputedProp base");
await om.DefineClassAsync("ComputedChild", "ComputedProp child", parentClass: "ComputedBase");
await om.DefineComputedPropAsync("ComputedBase", "risk_score");
om.RegisterComputedProp("ComputedBase", "risk_score", _ => ValueTask.FromResult<object?>(1));
await om.CreateObjectAsync("computedProp:child", "ComputedChild", "ComputedProp Child");
Assert(AsNumber(await om.GetFieldValueAsync("computedProp:child", "risk_score")) == 1, "child should inherit parent computedProp prop");
Assert((await om.GetObjectViewAsync("computedProp:child"))!.FieldValues.ContainsKey("risk_score"), "entity view should include inherited computedProp prop");
await om.DefineComputedPropAsync("ComputedChild", "risk_score");
om.RegisterComputedProp("ComputedChild", "risk_score", _ => ValueTask.FromResult<object?>(2));
Assert(AsNumber(await om.GetFieldValueAsync("computedProp:child", "risk_score")) == 2, "child computedProp prop should override parent computedProp prop");

await om.DefineClassAsync("ConstraintEmployee", "Constraint employee");
await om.DefineFieldAsync("ConstraintEmployee", "status", OmValueType.String);
await om.DefineFieldAsync("ConstraintEmployee", "end_date", OmValueType.String);
await om.DefineConstraintAsync(
    "ConstraintEmployee",
    "active_has_no_end_date",
    "conditional",
    async ctx => AsString(await ctx.GetFieldValueAsync("status")) == "active",
    async ctx =>
    {
        var endDate = AsString(await ctx.GetFieldValueAsync("end_date"));
        return string.IsNullOrWhiteSpace(endDate);
    },
    "end_date must be empty when status=active");
await om.CreateObjectAsync("constraint:emp", "ConstraintEmployee", "Constraint Emp");
await om.SetFieldValueAsync("constraint:emp", "status", "active");
await ExpectCozoExceptionAsync(
    () => om.SetFieldValueAsync("constraint:emp", "end_date", "2026-12-31"),
    "active_has_no_end_date",
    "conditional constraint should reject invalid property writes");
Assert(await om.GetFieldValueAsync("constraint:emp", "end_date") is null, "failed conditional constraint write should rollback property");

await om.DefineClassAsync("ConstraintDepartment", "Constraint department");
await om.DefineClassAsync("ConstraintStaff", "Constraint staff");
await om.DefineRelationDefAsync("constraint_heads", "ConstraintDepartment", "ConstraintStaff");
await om.DefineConstraintAsync(
    "ConstraintDepartment",
    "at_most_one_head",
    "cross-entity",
    _ => ValueTask.FromResult(true),
    async ctx => (await ctx.GetNeighborsAsync("constraint_heads", OmDirection.Outgoing)).Outgoing.Count <= 1,
    "department can have at most one head");
await om.CreateObjectAsync("constraint:dept", "ConstraintDepartment", "Constraint Dept");
await om.CreateObjectAsync("constraint:staff1", "ConstraintStaff", "Constraint Staff 1");
await om.CreateObjectAsync("constraint:staff2", "ConstraintStaff", "Constraint Staff 2");
await om.CreateRelationLinkAsync("constraint:dept", "constraint_heads", "constraint:staff1");
await ExpectCozoExceptionAsync(
    () => om.CreateRelationLinkAsync("constraint:dept", "constraint_heads", "constraint:staff2"),
    "at_most_one_head",
    "cross-entity constraint should reject invalid relation link writes");
Assert((await om.GetNeighborsAsync("constraint:dept", "constraint_heads", OmDirection.Outgoing)).Outgoing.Count == 1, "failed cross-entity constraint write should rollback relation link");

await om.DefineClassAsync("RiskAsset", "Risk asset");
await om.DefineFieldAsync("RiskAsset", "base_risk", OmValueType.Number);
await om.DefineFieldAsync("RiskAsset", "requires_review", OmValueType.Bool);
await om.DefineComputedPropAsync("RiskAsset", "risk_score");
om.RegisterComputedProp("RiskAsset", "risk_score", async ctx => (AsNumber(await ctx.GetFieldValueAsync("base_risk")) ?? 0) * 10);
await om.DefineConstraintAsync(
    "RiskAsset",
    "high_risk_requires_review",
    "computedProp-dep",
    async ctx => (AsNumber(await ctx.GetFieldValueAsync("risk_score")) ?? 0) > 80,
    async ctx => AsBool(await ctx.GetFieldValueAsync("requires_review")) == true,
    "requires_review must be true when risk_score > 80");
await om.CreateObjectAsync("risk:asset", "RiskAsset", "Risk Asset");
await om.SetFieldValueAsync("risk:asset", "base_risk", 9, new WriteOptions(SkipConstraints: true));
var riskValidation = await om.ValidateObjectAsync("risk:asset");
Assert(!riskValidation.Valid && riskValidation.Errors.Any(error => error.Contains("high_risk_requires_review", StringComparison.Ordinal)), "computedProp-dep constraint should see computedProp prop values");
await om.SetFieldValueAsync("risk:asset", "requires_review", true);
Assert((await om.ValidateObjectAsync("risk:asset")).Valid, "computedProp-dep constraint should pass after dependent property is set");

await om.DefineClassAsync("InheritedConstraintBase", "Inherited constraint base");
await om.DefineClassAsync("InheritedConstraintChild", "Inherited constraint child", parentClass: "InheritedConstraintBase");
await om.DefineFieldAsync("InheritedConstraintBase", "status", OmValueType.String);
await om.DefineFieldAsync("InheritedConstraintBase", "end_date", OmValueType.String);
await om.DefineConstraintAsync(
    "InheritedConstraintBase",
    "inherited_active_has_no_end_date",
    "conditional",
    async ctx => AsString(await ctx.GetFieldValueAsync("status")) == "active",
    async ctx => string.IsNullOrWhiteSpace(AsString(await ctx.GetFieldValueAsync("end_date"))),
    "end_date must be empty when inherited status=active");
await om.CreateObjectAsync("constraint:child", "InheritedConstraintChild", "Inherited Constraint Child");
await om.SetFieldValueAsync("constraint:child", "status", "active");
await ExpectCozoExceptionAsync(
    () => om.SetFieldValueAsync("constraint:child", "end_date", "2026-12-31"),
    "inherited_active_has_no_end_date",
    "subtype should inherit parent conditional constraints");

var people = await om.FindByClassAsync("Person");
Assert(people.Any(e => e.Id == "p1" && e.ClassName == "Employee"), "parent type query should include child entities");
var transientFailed = false;
try
{
    await om.CreateObjectAsync("transient", "TransientType", "Transient");
}
catch (CozoException)
{
    transientFailed = true;
}

Assert(transientFailed, "schema rollback should remove metadata added after snapshot");

await om.DefineConstraintAsync("Person", "reject_bad", "custom", "bad entity");
om.RegisterValidator("Person", "reject_bad", ctx =>
    ValueTask.FromResult(ctx.ObjectId == "p_bad" ? "custom failed" : null));
await om.CreateObjectAsync("p_bad", "Person", "Bad");
await om.SetFieldValueAsync("p_bad", "name", "Bad", new WriteOptions(SkipConstraints: true));
var customValidation = await om.ValidateObjectAsync("p_bad");
Assert(!customValidation.Valid && customValidation.Errors.Contains("custom failed"), "custom in-memory validator should run");

var actionLog = new List<string>();
await om.DefineMutationAsync(
    "Person",
    "setNameFromAction",
    async (ctx, parameters) =>
    {
        actionLog.Add("mutation");
        await ctx.SetFieldValueAsync("name", parameters["name"]);
    },
    "Set name from operation");
await om.DefineOperationAsync(
    "Person",
    "rename",
    (ctx, parameters) =>
    {
        actionLog.Add("operation");
        return ValueTask.FromResult<IReadOnlyList<MutationSpec>>(
        [
            new MutationSpec("setNameFromAction", new Dictionary<string, object?> { ["name"] = parameters["name"] })
        ]);
    },
    "Rename person");
await om.AddInterceptorAsync(
    "Person",
    "rename",
    "before",
    ctx =>
    {
        actionLog.Add("before");
        if (!ctx.Params.ContainsKey("name"))
        {
            throw new InvalidOperationException("name is required");
        }

        return ValueTask.CompletedTask;
    },
    "Require name");
await om.AddInterceptorAsync(
    "Person",
    "rename",
    "after",
    _ =>
    {
        actionLog.Add("after");
        return ValueTask.CompletedTask;
    },
    "Record after");
await om.ExecuteOperationAsync("p1", "rename", new Dictionary<string, object?> { ["name"] = "Alice Operation" });
Assert(string.Join(",", actionLog) == "before,operation,mutation,after", "operation pipeline should run before/operation/mutation/after in order");
Assert(AsString(await om.GetFieldValueAsync("p1", "name")) == "Alice Operation", "operation mutation should update property");

await om.DefineClassAsync("ParentOperationRoot", "Parent operation root");
await om.DefineClassAsync("ParentOperationMiddle", "Parent operation middle", parentClass: "ParentOperationRoot");
await om.DefineClassAsync("ParentOperationLeaf", "Parent operation leaf", parentClass: "ParentOperationMiddle");
await om.DefineFieldAsync("ParentOperationRoot", "parent_effect", OmValueType.String, required: false);
await om.DefineFieldAsync("ParentOperationRoot", "child_effect", OmValueType.String, required: false);
await om.CreateObjectAsync("parent-operation:middle", "ParentOperationMiddle", "Parent operation middle entity");
await om.CreateObjectAsync("parent-operation:leaf", "ParentOperationLeaf", "Parent operation leaf entity");
await om.SetFieldValueAsync("parent-operation:leaf", "parent_effect", "initial-parent");
await om.SetFieldValueAsync("parent-operation:leaf", "child_effect", "initial-child");

var directParentOwners = new List<string>();
var directParentInterceptorCount = 0;
await om.DefineOperationAsync(
    "ParentOperationRoot",
    "directParent",
    (ctx, _) =>
    {
        directParentOwners.Add(ctx.OperationOwnerClass);
        return ValueTask.FromResult<IReadOnlyList<MutationSpec>>([]);
    });
await om.DefineOperationAsync(
    "ParentOperationMiddle",
    "directParent",
    async (ctx, parameters) =>
    {
        directParentOwners.Add(ctx.OperationOwnerClass);
        return await ctx.CallParentOperationAsync("directParent", parameters);
    });
await om.AddInterceptorAsync(
    "ParentOperationRoot",
    "directParent",
    "before",
    _ =>
    {
        directParentInterceptorCount++;
        return ValueTask.CompletedTask;
    });
await om.ExecuteOperationAsync("parent-operation:middle", "directParent");
Assert(string.Join(",", directParentOwners) == "ParentOperationMiddle,ParentOperationRoot",
    "direct parent calls should advance OperationOwnerClass to the matched parent owner");
Assert(directParentInterceptorCount == 1, "calling a parent handler should not rerun inherited interceptors");

string? nearestInheritedOperationOwner = null;
await om.DefineOperationAsync(
    "ParentOperationMiddle",
    "nearestInheritedOperation",
    (ctx, _) =>
    {
        nearestInheritedOperationOwner = ctx.OperationOwnerClass;
        return ValueTask.FromResult<IReadOnlyList<MutationSpec>>([]);
    });
await om.ExecuteOperationAsync("parent-operation:leaf", "nearestInheritedOperation");
Assert(nearestInheritedOperationOwner == "ParentOperationMiddle",
    "a child entity should execute the nearest inherited operation registration");

var inheritedMutationOwners = new List<string>();
await om.DefineMutationAsync(
    "ParentOperationRoot",
    "nearestInheritedMutation",
    (_, _) =>
    {
        inheritedMutationOwners.Add("ParentOperationRoot");
        return ValueTask.CompletedTask;
    });
await om.DefineMutationAsync(
    "ParentOperationMiddle",
    "nearestInheritedMutation",
    async (ctx, _) =>
    {
        inheritedMutationOwners.Add("ParentOperationMiddle");
        await ctx.SetFieldValueAsync("parent_effect", "nearest-middle-mutation");
    });
await om.DefineOperationAsync(
    "ParentOperationLeaf",
    "returnInheritedMutation",
    (_, _) => ValueTask.FromResult<IReadOnlyList<MutationSpec>>([new MutationSpec("nearestInheritedMutation")]));
await om.ExecuteOperationAsync("parent-operation:leaf", "returnInheritedMutation");
Assert(string.Join(",", inheritedMutationOwners) == "ParentOperationMiddle",
    "a child operation mutation should resolve to the nearest ancestor registration");
Assert(AsString(await om.GetFieldValueAsync("parent-operation:leaf", "parent_effect")) == "nearest-middle-mutation",
    "the nearest inherited mutation should commit through the child operation transaction");
await om.SetFieldValueAsync("parent-operation:leaf", "parent_effect", "initial-parent");

await om.DefineMutationAsync(
    "ParentOperationRoot",
    "setParentEffect",
    async (ctx, parameters) => await ctx.SetFieldValueAsync("parent_effect", parameters["value"]));
await om.DefineMutationAsync(
    "ParentOperationLeaf",
    "setChildEffect",
    async (ctx, parameters) => await ctx.SetFieldValueAsync("child_effect", parameters["value"]));
await om.DefineMutationAsync(
    "ParentOperationLeaf",
    "failAfterEffects",
    (_, _) => throw new InvalidOperationException("parent operation effect failure"));

var layeredParentOwners = new List<string>();
var parentEffectsWereDeferred = false;
await om.DefineOperationAsync(
    "ParentOperationRoot",
    "layeredEffects",
    (ctx, parameters) =>
    {
        layeredParentOwners.Add(ctx.OperationOwnerClass);
        return ValueTask.FromResult<IReadOnlyList<MutationSpec>>(
        [
            new MutationSpec("setParentEffect", new Dictionary<string, object?> { ["value"] = parameters["parent"] })
        ]);
    });
await om.DefineOperationAsync(
    "ParentOperationMiddle",
    "layeredEffects",
    async (ctx, parameters) =>
    {
        layeredParentOwners.Add(ctx.OperationOwnerClass);
        return await ctx.CallParentOperationAsync("layeredEffects", parameters);
    });
await om.DefineOperationAsync(
    "ParentOperationLeaf",
    "layeredEffects",
    async (ctx, parameters) =>
    {
        layeredParentOwners.Add(ctx.OperationOwnerClass);
        var parentMutations = await ctx.CallParentOperationAsync("layeredEffects", parameters);
        parentEffectsWereDeferred = AsString(await ctx.GetFieldValueAsync("parent_effect")) == "initial-parent";
        return
        [
            .. parentMutations,
            new MutationSpec("setChildEffect", new Dictionary<string, object?> { ["value"] = parameters["child"] })
        ];
    });

await om.ExecuteOperationAsync(
    "parent-operation:leaf",
    "layeredEffects",
    new Dictionary<string, object?> { ["parent"] = "parent-applied", ["child"] = "child-applied" });
Assert(string.Join(",", layeredParentOwners) == "ParentOperationLeaf,ParentOperationMiddle,ParentOperationRoot",
    "three-level parent calls should visit each override exactly once");
Assert(parentEffectsWereDeferred, "parent handlers should return mutations without executing them");
Assert(AsString(await om.GetFieldValueAsync("parent-operation:leaf", "parent_effect")) == "parent-applied"
       && AsString(await om.GetFieldValueAsync("parent-operation:leaf", "child_effect")) == "child-applied",
    "outer operation execution should apply combined parent and child mutations");

await om.DefineOperationAsync(
    "ParentOperationLeaf",
    "missingParent",
    async (ctx, parameters) =>
    {
        await ctx.SetFieldValueAsync("child_effect", "missing-parent-should-roll-back");
        return await ctx.CallParentOperationAsync("missingParent", parameters);
    });
var missingParentDiagnosed = false;
try
{
    await om.ExecuteOperationAsync("parent-operation:leaf", "missingParent");
}
catch (InvalidOperationException ex) when (
    ex.Message.Contains("missingParent", StringComparison.Ordinal)
    && ex.Message.Contains("ParentOperationLeaf", StringComparison.Ordinal))
{
    missingParentDiagnosed = true;
}

Assert(missingParentDiagnosed, "missing parent operation diagnostics should contain the operation and current owner");
Assert(AsString(await om.GetFieldValueAsync("parent-operation:leaf", "child_effect")) == "child-applied",
    "a missing parent operation should roll back earlier writes in the outer operation transaction");

await om.DefineOperationAsync(
    "ParentOperationLeaf",
    "layeredEffectsThenFail",
    async (ctx, parameters) =>
    {
        var combined = await ctx.CallParentOperationAsync("layeredEffects", parameters);
        return
        [
            .. combined,
            new MutationSpec("setChildEffect", new Dictionary<string, object?> { ["value"] = parameters["child"] }),
            new MutationSpec("failAfterEffects")
        ];
    });
await ExpectExceptionAsync<InvalidOperationException>(
    () => om.ExecuteOperationAsync(
        "parent-operation:leaf",
        "layeredEffectsThenFail",
        new Dictionary<string, object?> { ["parent"] = "rolled-back-parent", ["child"] = "rolled-back-child" }),
    "a later mutation failure should abort parent and child effects");
Assert(AsString(await om.GetFieldValueAsync("parent-operation:leaf", "parent_effect")) == "parent-applied"
       && AsString(await om.GetFieldValueAsync("parent-operation:leaf", "child_effect")) == "child-applied",
    "parent and child mutation effects should roll back together when the outer operation fails");

await om.DefineClassAsync("InterceptorOrderParent", "Interceptor order parent");
await om.DefineClassAsync("InterceptorOrderChild", "Interceptor order child", parentClass: "InterceptorOrderParent");
await om.DefineFieldAsync("InterceptorOrderParent", "interceptor_value", OmValueType.String, required: false);
await om.CreateObjectAsync("interceptor-order:child", "InterceptorOrderChild", "Interceptor order child entity");
await om.SetFieldValueAsync("interceptor-order:child", "interceptor_value", "initial");
await om.DefineMutationAsync(
    "InterceptorOrderChild",
    "setInterceptorValue",
    async (ctx, parameters) => await ctx.SetFieldValueAsync("interceptor_value", parameters["value"]));

var inheritedInterceptorOrder = new List<string>();
await om.DefineOperationAsync(
    "InterceptorOrderChild",
    "orderedInterceptors",
    (_, _) =>
    {
        inheritedInterceptorOrder.Add("operation");
        return ValueTask.FromResult<IReadOnlyList<MutationSpec>>(
        [
            new MutationSpec(
                "setInterceptorValue",
                new Dictionary<string, object?> { ["value"] = "ordered" })
        ]);
    });
await om.AddInterceptorAsync("InterceptorOrderParent", "orderedInterceptors", "before", _ =>
{
    inheritedInterceptorOrder.Add("parent-before-1");
    return ValueTask.CompletedTask;
});
await om.AddInterceptorAsync("InterceptorOrderParent", "orderedInterceptors", "before", _ =>
{
    inheritedInterceptorOrder.Add("parent-before-2");
    return ValueTask.CompletedTask;
});
await om.AddInterceptorAsync("InterceptorOrderChild", "orderedInterceptors", "before", _ =>
{
    inheritedInterceptorOrder.Add("child-before-1");
    return ValueTask.CompletedTask;
});
await om.AddInterceptorAsync("InterceptorOrderChild", "orderedInterceptors", "before", _ =>
{
    inheritedInterceptorOrder.Add("child-before-2");
    return ValueTask.CompletedTask;
});
await om.AddInterceptorAsync("InterceptorOrderParent", "orderedInterceptors", "after", _ =>
{
    inheritedInterceptorOrder.Add("parent-after-1");
    return ValueTask.CompletedTask;
});
await om.AddInterceptorAsync("InterceptorOrderParent", "orderedInterceptors", "after", _ =>
{
    inheritedInterceptorOrder.Add("parent-after-2");
    return ValueTask.CompletedTask;
});
await om.AddInterceptorAsync("InterceptorOrderChild", "orderedInterceptors", "after", _ =>
{
    inheritedInterceptorOrder.Add("child-after-1");
    return ValueTask.CompletedTask;
});
await om.AddInterceptorAsync("InterceptorOrderChild", "orderedInterceptors", "after", _ =>
{
    inheritedInterceptorOrder.Add("child-after-2");
    return ValueTask.CompletedTask;
});
await om.ExecuteOperationAsync("interceptor-order:child", "orderedInterceptors");
Assert(
    string.Join(",", inheritedInterceptorOrder) ==
    "parent-before-1,parent-before-2,child-before-1,child-before-2,operation,parent-after-1,parent-after-2,child-after-1,child-after-2",
    "inherited before and after interceptors should preserve parent grouping and owner-local registration order");
Assert(AsString(await om.GetFieldValueAsync("interceptor-order:child", "interceptor_value")) == "ordered",
    "ordered interceptor operation should commit its mutation");

var inheritedBeforeActionRan = false;
var inheritedBeforeMutationRan = false;
await om.DefineMutationAsync(
    "InterceptorOrderChild",
    "setValueAfterInheritedBefore",
    async (ctx, _) =>
    {
        inheritedBeforeMutationRan = true;
        await ctx.SetFieldValueAsync("interceptor_value", "mutation-should-not-run");
    });
await om.DefineOperationAsync(
    "InterceptorOrderChild",
    "inheritedBeforeFailure",
    (_, _) =>
    {
        inheritedBeforeActionRan = true;
        return ValueTask.FromResult<IReadOnlyList<MutationSpec>>([new MutationSpec("setValueAfterInheritedBefore")]);
    });
await om.AddInterceptorAsync(
    "InterceptorOrderParent",
    "inheritedBeforeFailure",
    "before",
    async ctx =>
    {
        await ctx.SetFieldValueAsync("interceptor_value", "before-should-roll-back");
        throw new InvalidOperationException("inherited before failed");
    });
await ExpectExceptionAsync<InvalidOperationException>(
    () => om.ExecuteOperationAsync("interceptor-order:child", "inheritedBeforeFailure"),
    "inherited before interceptor failures should abort operation execution");
Assert(!inheritedBeforeActionRan && !inheritedBeforeMutationRan,
    "an inherited before failure should prevent both the operation and its mutations from running");
Assert(AsString(await om.GetFieldValueAsync("interceptor-order:child", "interceptor_value")) == "ordered",
    "an inherited before failure should leave no committed interceptor, operation, or mutation effects");

var inheritedAfterActionRan = false;
var inheritedAfterMutationRan = false;
await om.DefineMutationAsync(
    "InterceptorOrderChild",
    "setValueBeforeInheritedAfter",
    async (ctx, _) =>
    {
        inheritedAfterMutationRan = true;
        await ctx.SetFieldValueAsync("interceptor_value", "after-should-roll-back");
    });
await om.DefineOperationAsync(
    "InterceptorOrderChild",
    "inheritedAfterFailure",
    (_, _) =>
    {
        inheritedAfterActionRan = true;
        return ValueTask.FromResult<IReadOnlyList<MutationSpec>>([new MutationSpec("setValueBeforeInheritedAfter")]);
    });
await om.AddInterceptorAsync(
    "InterceptorOrderParent",
    "inheritedAfterFailure",
    "after",
    _ => throw new InvalidOperationException("inherited after failed"));
await ExpectExceptionAsync<InvalidOperationException>(
    () => om.ExecuteOperationAsync("interceptor-order:child", "inheritedAfterFailure"),
    "inherited after interceptor failures should abort the operation transaction");
Assert(inheritedAfterActionRan && inheritedAfterMutationRan,
    "an inherited after failure should occur after the operation and mutation have run");
Assert(AsString(await om.GetFieldValueAsync("interceptor-order:child", "interceptor_value")) == "ordered",
    "an inherited after failure should roll back the complete operation transaction");

var rollbackActionLog = new List<string>();
await om.DefineMutationAsync(
    "Person",
    "setNameBeforeFail",
    async (ctx, parameters) =>
    {
        rollbackActionLog.Add("mutation");
        await ctx.SetFieldValueAsync("name", parameters["name"]);
    },
    "Set name before failing after interceptor");
await om.DefineOperationAsync(
    "Person",
    "renameThenFail",
    (_, _) =>
    {
        rollbackActionLog.Add("operation");
        return ValueTask.FromResult<IReadOnlyList<MutationSpec>>(
        [
            new MutationSpec("setNameBeforeFail", new Dictionary<string, object?> { ["name"] = "Should Roll Back" })
        ]);
    },
    "Rename and fail");
await om.AddInterceptorAsync(
    "Person",
    "renameThenFail",
    "after",
    _ =>
    {
        rollbackActionLog.Add("after");
        throw new InvalidOperationException("after failed");
    },
    "Fail after");
var actionRolledBack = false;
try
{
    await om.ExecuteOperationAsync("p1", "renameThenFail");
}
catch (InvalidOperationException ex) when (ex.Message.Contains("after failed", StringComparison.Ordinal))
{
    actionRolledBack = true;
}

Assert(actionRolledBack, "operation pipeline should surface after interceptor failure");
Assert(string.Join(",", rollbackActionLog) == "operation,mutation,after", "failing operation should run until failing after interceptor");
Assert(AsString(await om.GetFieldValueAsync("p1", "name")) == "Alice Operation", "failing operation should rollback mutation writes");

await om.CreateObjectAsync("admin", "Person", "Permission admin");
await om.CreateObjectAsync("u1", "Person", "Permission user");
await om.DefineRelationDefAsync("permission_can_read_person", "Person", "Person");
await om.DefineRelationDefAsync("permission_can_inspect_person", "Person", "Person");
await om.CreateRelationLinkAsync("admin", "assigned_project", "proj1");
await om.CreateRelationLinkAsync("u1", "permission_can_read_person", "p1");
await om.CreateRelationLinkAsync("admin", "permission_can_inspect_person", "p1");
await om.DefineFieldAsync("Person", "is_admin", OmValueType.Bool);
await om.SetFieldValueAsync("admin", "is_admin", true, new WriteOptions(SkipConstraints: true));
await om.SetFieldValueAsync("u1", "is_admin", false, new WriteOptions(SkipConstraints: true));
await om.SeedPermissionMetadataAsync();
await PermissionGovernanceParityFixtures.RunAsync();

await om.SeedPermissionMetadataAsync(new PermissionSeedInput(
    Operations: [new PermissionOperationSeed("inspect", "Inspect resource")],
    Policies: [new PermissionPolicySeed("allow_admin_inspect_project", "allow", "inspect", "Project")],
    AbacRules: [new PermissionAbacRuleSeed("allow_admin_inspect_project", "subject.is_admin", "=", "true")],
    PathRules: [new PermissionPathRuleSeed("allow_admin_inspect_project", "assigned_project")]));
Assert((await om.CheckAccessAsync(new CheckAccessInput("admin", "inspect", "proj1"))).Allow, "permission seed input should load policies and abac rules");
Assert(!(await om.CheckAccessAsync(new CheckAccessInput("u1", "inspect", "proj1"))).Allow, "permission seed abac mismatch should deny access");
await om.DefinePermissionPolicyAsync("allow_person_read", "allow", "read", "Person");
await om.AddPermissionPathRuleAsync("allow_person_read", "permission_can_read_person");
Assert((await om.CheckAccessAsync(new CheckAccessInput("u1", "read", "p1"))).Allow, "allow policy should grant access");
var readExplanation = (await om.CheckAccessAsync(new CheckAccessInput("u1", "read", "p1"))).Explanation.GetRawText();
Assert(readExplanation.Contains("permission_can_read_person", StringComparison.Ordinal), "permission explanation should include the canonical witness path");
await om.DefinePermissionPolicyAsync("allow_person_name_inspect", "allow", "inspect", "Person.name");
await om.AddPermissionAbacRuleAsync("allow_person_name_inspect", "subject.is_admin", "=", "true");
await om.AddPermissionPathRuleAsync("allow_person_name_inspect", "permission_can_inspect_person");
Assert((await om.CheckAccessAsync(new CheckAccessInput("admin", "inspect", "p1", FieldName: "name"))).Allow, "field policy should grant matching field");
Assert(!(await om.CheckAccessAsync(new CheckAccessInput("u1", "inspect", "p1", FieldName: "name"))).Allow, "ABAC mismatch should deny field access");
await om.DefinePermissionPolicyAsync("deny_person_read", "deny", "read", "Person");
await om.AddPermissionPathRuleAsync("deny_person_read", "permission_can_read_person");
Assert(!(await om.CheckAccessAsync(new CheckAccessInput("u1", "read", "p1"))).Allow, "deny policy should override allow");

await om.DefineClassAsync("Order", "Order");
await om.DefineClassAsync("Shipment", "Shipment");
await om.DefineFieldAsync("Shipment", "carrier", OmValueType.String);
await om.DefineRelationDefAsync("has_shipment", "Order", "Shipment");
await om.CreateObjectAsync("o1", "Order", "Order 1");
await om.DefineExistentialRuleAsync(
    "order_has_shipment",
    new ExistentialRuleSpec(
        new ExistentialForEachSpec("Order"),
        new ExistentialExistsSpec("has_shipment", ExistentialDirection.Out, "Shipment"),
        new ExistentialMaterializeSpec("shipment:{fromId}", new Dictionary<string, JsonElement>
        {
            ["carrier"] = JsonSerializer.SerializeToElement("pending")
        }),
        ExistentialRuleMode.Materialize,
        "order must have shipment"));

var violations = await om.CheckExistentialRulesAsync();
Assert(violations.Count == 1 && violations[0].ObjectId == "o1", "existential check should report missing relation link");

var chase = await om.ApplyExistentialRulesAsync(new ApplyExistentialRulesInput(MaxIterations: 3));
Assert(chase.Created.Count == 1 && chase.ReachedFixpoint, "existential materialization should create one Skolem relation link and reach fixpoint");
Assert((await om.CheckExistentialRulesAsync()).Count == 0, "existential materialization should be idempotent after apply");
Assert((await om.ApplyExistentialRulesAsync(new ApplyExistentialRulesInput(MaxIterations: 3))).Created.Count == 0, "second existential apply should not duplicate Skolem objects");

var shipmentNeighbors = await om.GetNeighborsAsync("o1", "has_shipment", OmDirection.Outgoing);
Assert(shipmentNeighbors.Outgoing.Count == 1 && shipmentNeighbors.Outgoing[0].ClassName == "Shipment", "Skolem shipment should be linked");

await om.DefineClassAsync("BatchPerson", "Batch person");
await om.DefineClassAsync("BatchDepartment", "Batch department");
await om.DefineFieldAsync("BatchPerson", "name", OmValueType.String, required: true);
await om.DefineFieldAsync("BatchDepartment", "title", OmValueType.String, required: true);
await om.DefineRelationDefAsync("batch_works_in", "BatchPerson", "BatchDepartment");

var batchResult = await om.IngestBatchAsync(new OmBatchInput(
    Objects:
    [
        new OmBatchObject("bp1", "BatchPerson", "Batch Alice"),
        new OmBatchObject("bd1", "BatchDepartment", "Batch Engineering")
    ],
    FieldValues:
    [
        new OmBatchFieldValue("bp1", "name", "Batch Alice"),
        new OmBatchFieldValue("bd1", "title", "Batch Engineering")
    ],
    RelationLinks:
    [
        new OmBatchRelationLink("bp1", "batch_works_in", "bd1", new { source = "test" })
    ]));
Assert(batchResult is { Objects: 2, FieldValues: 2, RelationLinks: 1, ValidatedObjects: 2 }, "batch ingest should return Object-compatible counts");
Assert(AsString(await om.GetFieldValueAsync("bp1", "name")) == "Batch Alice", "batch property should be written");
Assert((await om.GetNeighborsAsync("bp1", "batch_works_in", OmDirection.Outgoing)).Outgoing.Count == 1, "batch relation link should be written");

var requiredFailed = false;
try
{
    await om.IngestBatchAsync(new OmBatchInput(Objects: [new OmBatchObject("bp_missing", "BatchPerson", "Missing required")]));
}
catch (CozoException ex) when (ex.Message.Contains("name", StringComparison.Ordinal))
{
    requiredFailed = true;
}

Assert(requiredFailed, "batch ingest should validate touched objects by default");
var batchRolledBack = false;
try
{
    await om.GetObjectClassAsync("bp_missing");
}
catch (CozoException)
{
    batchRolledBack = true;
}

Assert(batchRolledBack, "failed batch ingest should rollback created objects");

var skippedResult = await om.IngestBatchAsync(
    new OmBatchInput(Objects: [new OmBatchObject("bp_skip", "BatchPerson", "Skip required")]),
    new OmBatchOptions(ValidateRequired: false));
Assert(skippedResult.ValidatedObjects == 1 && (await om.GetObjectClassAsync("bp_skip")) == "BatchPerson", "batch ingest can skip required validation while preserving Object-compatible count");

await om.DefineClassAsync("AnalyticsNode", "Analytics node");
await om.DefineFieldAsync("AnalyticsNode", "risk", OmValueType.Number);
await om.DefineRelationDefAsync("analytics_link", "AnalyticsNode", "AnalyticsNode");
await om.IngestBatchAsync(new OmBatchInput(
    Objects:
    [
        new OmBatchObject("an_root", "AnalyticsNode", "Root"),
        new OmBatchObject("an_child", "AnalyticsNode", "Child"),
        new OmBatchObject("an_other", "AnalyticsNode", "Other")
    ],
    FieldValues:
    [
        new OmBatchFieldValue("an_root", "risk", 10),
        new OmBatchFieldValue("an_child", "risk", 50),
        new OmBatchFieldValue("an_other", "risk", 30)
    ],
    RelationLinks:
    [
        new OmBatchRelationLink("an_root", "analytics_link", "an_child"),
        new OmBatchRelationLink("an_root", "analytics_link", "an_other")
    ]));

var impact = await om.ImpactAnalysisAsync(new ImpactAnalysisInput(
    "an_root",
    ["analytics_link"],
    MaxDepth: 2,
    Direction: OmAnalyticsDirection.Outgoing));
Assert(impact.Template == "impactAnalysis" && impact.Stats.ImpactedCount == 2, "impact analysis should return graph stats");
Assert(impact.Data.Visual.Graph.Nodes.Any(n => n.Id == "an_root" && n.Flags.IsRoot), "impact analysis visual graph should flag root");
Assert(impact.Data.Visual.Graph.Edges.Count >= 2, "impact analysis visual graph should include edges");

var ownership = await om.OwnershipTreeAsync(new OwnershipTreeInput("an_root", ["analytics_link"], MaxDepth: 2));
Assert(ownership.Template == "ownershipTree" && ownership.Data.Visual.Tree.RootId == "an_root", "ownership tree should return root tree visual");
Assert(ownership.Data.Visual.Tree.ChildrenById["an_root"].Count == 2, "ownership tree should group children by root id");
Assert(ownership.Data.Visual.Graph.Nodes.Count >= 3, "ownership tree should include graph visual");

var hotspots = await om.RiskHotspotAsync(new RiskHotspotInput("AnalyticsNode", "risk", TopK: 2, DegreeWeight: 1));
Assert(hotspots.Template == "riskHotspot" && hotspots.Stats.ReturnedCount == 2, "risk hotspot should return ranking stats");
Assert(hotspots.Data.Visual.Ranking[0].Score >= hotspots.Data.Visual.Ranking[1].Score, "risk hotspot ranking should sort descending");
Assert(hotspots.Data.Visual.Ranking[0].Id == "an_child" && hotspots.Data.Visual.Ranking[1].Id == "an_other", "risk hotspot should rank by risk plus degree");

HarnessDiagnostics.Start("Jint behavior runtime contract matrix");
await OmScriptingJintContractTests.RunAsync();

HarnessDiagnostics.Complete();
Console.WriteLine("Depa.Ontology integration tests passed.");

/// <summary>Fixed clock for the timeprovider-indexed-at case (track fix-om-depa-conformance-gaps).</summary>
file sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => now;
}

file sealed class ScriptFailingOmStore(ICozoOmStore inner) : ICozoOmStore
{
    public string? FailWhenScriptContains { get; set; }
    public bool FailBeginTransaction { get; set; }

    public Task<OmQueryResult> RunAsync(
        string script,
        object? parameters = null,
        bool immutable = false,
        CancellationToken cancellationToken = default)
    {
        if (FailWhenScriptContains is not null
            && script.Contains(FailWhenScriptContains, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("simulated persistent metadata failure");
        }

        return inner.RunAsync(script, parameters, immutable, cancellationToken);
    }

    public Task<ICozoOmTransaction> BeginTransactionAsync(
        bool write = true,
        CancellationToken cancellationToken = default)
    {
        if (FailBeginTransaction)
        {
            throw new InvalidOperationException("simulated transaction start failure");
        }

        return inner.BeginTransactionAsync(write, cancellationToken);
    }
}

file sealed class RunHookOmStore(ICozoOmStore inner) : ICozoOmStore
{
    public string? RunOnceWhenScriptContains { get; set; }
    public Action? OnRunOnce { get; set; }

    public Task<OmQueryResult> RunAsync(
        string script,
        object? parameters = null,
        bool immutable = false,
        CancellationToken cancellationToken = default)
    {
        if (RunOnceWhenScriptContains is not null
            && script.Contains(RunOnceWhenScriptContains, StringComparison.Ordinal))
        {
            RunOnceWhenScriptContains = null;
            var callback = OnRunOnce;
            OnRunOnce = null;
            callback?.Invoke();
        }

        return inner.RunAsync(script, parameters, immutable, cancellationToken);
    }

    public Task<ICozoOmTransaction> BeginTransactionAsync(
        bool write = true,
        CancellationToken cancellationToken = default) =>
        inner.BeginTransactionAsync(write, cancellationToken);
}

file sealed record BehaviorMetadataProbe(
    BehaviorCatalogKind Kind,
    string OwnerClass,
    string Name,
    string? ConstraintKind,
    string? Message,
    string? Description,
    string? InterceptorPhase,
    int? InterceptorSeq);

file sealed record BehaviorReadinessProbe(
    BehaviorCatalogKind Kind,
    string OwnerClass,
    string Name,
    string? InterceptorPhase,
    int? InterceptorSeq,
    BehaviorCatalogCallbackSlot Slot,
    string? BindingId,
    BehaviorReadiness Readiness);

file sealed record RegistryBindingProbe(
    BehaviorCatalogKind Kind,
    string OwnerClass,
    string Name,
    BehaviorCatalogCallbackSlot Slot,
    string? Phase,
    int? Seq,
    string? BindingId,
    string? Description);

file sealed record BehaviorStateProbe(
    ImmutableArray<byte> CanonicalCatalog,
    ImmutableArray<BehaviorMetadataProbe> Metadata,
    ImmutableArray<BehaviorBindingRow> Bindings,
    ImmutableArray<RegistryBindingProbe> RegistryBindings,
    ImmutableArray<BehaviorReadinessProbe> Readiness,
    CozoOmRegistrySnapshot RegistrySnapshot);
