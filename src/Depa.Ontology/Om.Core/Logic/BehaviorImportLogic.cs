using System.Collections.Immutable;
using Depa.Ontology.Contracts;
using Depa.Ontology.Contracts.Models;
using Depa.Ontology.Runtime;
using Depa.Ontology.Support;

namespace Depa.Ontology.Logic;

internal static class BehaviorImportLogic
{
    private static readonly Func<OmValidationContext, ValueTask<bool>> MissingConstraint =
        _ => ValueTask.FromException<bool>(new InvalidOperationException("Unresolved imported constraint callback was invoked."));

    internal static async Task<BehaviorImportResult> ImportJsonAsync(
        CozoOmRuntime runtime,
        string json,
        BehaviorCallbackBindingSet? callbacks,
        BehaviorImportOptions? options,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(runtime);
        callbacks ??= new BehaviorCallbackBindingSet();
        options ??= new BehaviorImportOptions();

        cancellationToken.ThrowIfCancellationRequested();
        var decoded = BehaviorManifestJsonCodec.Decode(json);
        if (!decoded.Success)
        {
            return new BehaviorImportResult(false, decoded.Diagnostics.Select(diagnostic =>
                new BehaviorImportDiagnostic(diagnostic.Code, diagnostic.Path, diagnostic.Message)));
        }

        var callbackIndex = CallbackIndex.Create(callbacks);
        var diagnostics = callbackIndex.Diagnostics.ToList();
        if (diagnostics.Count > 0)
        {
            return new BehaviorImportResult(false, diagnostics);
        }

        await using var gate = await runtime.BehaviorGate.EnterAsync(cancellationToken).ConfigureAwait(false);
        var catalog = decoded.Catalog!;
        diagnostics.AddRange(await ValidateCatalogAsync(runtime, catalog, cancellationToken).ConfigureAwait(false));
        if (diagnostics.Count > 0)
        {
            return new BehaviorImportResult(false, diagnostics);
        }

        var preRegistry = runtime.Registry.CaptureSnapshot();
        var (stagedRegistry, unresolved, readinessDiagnostics) = StageRegistry(preRegistry, catalog, callbackIndex);
        if (options.RequireReady && (unresolved.Length > 0 || readinessDiagnostics.Length > 0))
        {
            return new BehaviorImportResult(false, readinessDiagnostics, unresolved);
        }

        diagnostics.AddRange(readinessDiagnostics);

        var currentCatalog = await BehaviorCatalogLogic.GetResolvedAsync(
            runtime,
            new BehaviorResolutionScope(preRegistry),
            cancellationToken).ConfigureAwait(false);
        var currentBindings = await BehaviorBindingLogic.ListAsync(runtime, cancellationToken).ConfigureAwait(false);
        var affectedEntries = currentCatalog.Behaviors.Where(current =>
            catalog.Behaviors.Any(imported => SameBehaviorKey(current, imported))).ToImmutableArray();
        var affectedBindings = currentBindings.Where(row =>
            catalog.Behaviors.Any(entry => SameBehaviorKey(row.Key, entry))).ToImmutableArray();

        try
        {
            await using var tx = await runtime.Store.BeginTransactionAsync(write: true, cancellationToken).ConfigureAwait(false);
            var txRuntime = runtime with { Store = tx };
            await ApplyPersistentAsync(txRuntime, catalog.Behaviors, cancellationToken).ConfigureAwait(false);
            if (runtime.BehaviorImportFaults.BeforePersistentCommitAsync is { } beforeCommit)
            {
                await beforeCommit(cancellationToken).ConfigureAwait(false);
            }

            await tx.CommitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception failure)
        {
            throw new BehaviorImportException("Behavior manifest persistence failed before registry publication.", failure);
        }

        try
        {
            if (runtime.BehaviorImportFaults.AfterPersistentCommitAsync is { } afterCommit)
            {
                await afterCommit().ConfigureAwait(false);
            }

            runtime.BehaviorImportFaults.BeforeRegistryPublish?.Invoke();
            gate.PublishRegistrySnapshot(runtime.Registry, preRegistry, stagedRegistry);
            return new BehaviorImportResult(true, diagnostics, unresolved);
        }
        catch (Exception publishFailure)
        {
            var compensationFailures = new List<Exception>();
            try
            {
                if (runtime.BehaviorImportFaults.BeforePersistentCompensationAsync is { } beforeCompensation)
                {
                    await beforeCompensation().ConfigureAwait(false);
                }

                await using var compensation = await runtime.Store.BeginTransactionAsync(write: true).ConfigureAwait(false);
                var compensationRuntime = runtime with { Store = compensation };
                await RestorePersistentAsync(
                    compensationRuntime,
                    catalog.Behaviors,
                    affectedEntries,
                    affectedBindings,
                    CancellationToken.None).ConfigureAwait(false);
                await compensation.CommitAsync().ConfigureAwait(false);
            }
            catch (Exception compensationFailure)
            {
                compensationFailures.Add(compensationFailure);
            }

            throw new BehaviorImportException(
                compensationFailures.Count == 0
                    ? "Behavior registry publication failed; persistent state was restored."
                    : "Behavior registry publication failed and persistent compensation was incomplete.",
                publishFailure,
                compensationFailures);
        }
    }

    private static async Task<ImmutableArray<BehaviorImportDiagnostic>> ValidateCatalogAsync(
        CozoOmRuntime runtime,
        BehaviorCatalog catalog,
        CancellationToken cancellationToken)
    {
        var diagnostics = ImmutableArray.CreateBuilder<BehaviorImportDiagnostic>();
        foreach (var entry in catalog.Behaviors)
        {
            var resolvedOwner = await ClassLogic.ResolveClassAsync(runtime, entry.OwnerClass, cancellationToken).ConfigureAwait(false);
            if (!string.Equals(resolvedOwner, entry.OwnerClass, StringComparison.Ordinal)
                || !await ClassLogic.ClassExistsAsync(runtime, resolvedOwner, cancellationToken).ConfigureAwait(false))
            {
                diagnostics.Add(Diagnostic("OMI1101", entry, null, null, $"Owner class '{entry.OwnerClass}' does not exist as a canonical type."));
            }

            if (entry.Kind == BehaviorCatalogKind.Constraint)
            {
                var constraintKind = entry.ConstraintKind?.Trim().ToLowerInvariant();
                if (constraintKind is not ("conditional" or "cross-entity" or "computedprop-dep" or "custom"))
                {
                    diagnostics.Add(Diagnostic("OMI1102", entry, null, null, $"Constraint kind '{entry.ConstraintKind}' is unsupported."));
                }
                else
                {
                    ValidateConstraintSlots(entry, constraintKind, diagnostics);
                }
            }

            if (entry.Callbacks.IsEmpty)
            {
                diagnostics.Add(Diagnostic("OMI1103", entry, null, null, "Behavior entry must declare at least one callback slot."));
            }
        }

        return diagnostics.ToImmutable();
    }

    private static void ValidateConstraintSlots(
        BehaviorCatalogEntry entry,
        string constraintKind,
        ImmutableArray<BehaviorImportDiagnostic>.Builder diagnostics)
    {
        var expected = constraintKind == "custom"
            ? new[] { BehaviorCatalogCallbackSlot.Validator }
            : new[] { BehaviorCatalogCallbackSlot.When, BehaviorCatalogCallbackSlot.Then };
        var actual = entry.Callbacks.Select(callback => callback.Slot).ToHashSet();
        var missing = expected.Where(slot => !actual.Contains(slot)).ToArray();
        var extra = ConstraintSlotOrder.Where(slot => actual.Contains(slot) && !expected.Contains(slot)).ToArray();
        if (missing.Length == 0 && extra.Length == 0)
        {
            return;
        }

        diagnostics.Add(new BehaviorImportDiagnostic(
            "OMI1104",
            $"{BehaviorKey(entry)}.callbacks",
            $"Constraint kind '{constraintKind}' requires exactly callback slots [{FormatSlots(expected)}]; "
            + $"missing slots [{FormatSlots(missing)}]; extra slots [{FormatSlots(extra)}].",
            entry.Kind,
            entry.OwnerClass,
            entry.Name));
    }

    private static readonly BehaviorCatalogCallbackSlot[] ConstraintSlotOrder =
    [
        BehaviorCatalogCallbackSlot.When,
        BehaviorCatalogCallbackSlot.Then,
        BehaviorCatalogCallbackSlot.Validator,
    ];

    private static string FormatSlots(IEnumerable<BehaviorCatalogCallbackSlot> slots)
    {
        var names = slots.Select(slot => slot switch
        {
            BehaviorCatalogCallbackSlot.When => "when",
            BehaviorCatalogCallbackSlot.Then => "then",
            BehaviorCatalogCallbackSlot.Validator => "validator",
            _ => throw new ArgumentOutOfRangeException(nameof(slots), slot, "Unknown constraint callback slot."),
        }).ToArray();
        return names.Length == 0 ? "none" : string.Join(", ", names);
    }

    private static (CozoOmRegistrySnapshot Snapshot, ImmutableArray<BehaviorUnresolvedDiagnostic> Unresolved, ImmutableArray<BehaviorImportDiagnostic> Diagnostics)
        StageRegistry(CozoOmRegistrySnapshot source, BehaviorCatalog catalog, CallbackIndex callbacks)
    {
        var snapshot = source;
        var unresolved = ImmutableArray.CreateBuilder<BehaviorUnresolvedDiagnostic>();
        var diagnostics = ImmutableArray.CreateBuilder<BehaviorImportDiagnostic>();

        foreach (var entry in catalog.Behaviors)
        {
            if (entry.Kind == BehaviorCatalogKind.Constraint)
            {
                snapshot = StageConstraint(snapshot, entry, callbacks, unresolved, diagnostics);
                continue;
            }

            foreach (var callback in entry.Callbacks)
            {
                var bindingId = callback.BindingId;
                var key = (entry.OwnerClass, entry.Name);
                switch (entry.Kind)
                {
                    case BehaviorCatalogKind.ComputedProp:
                        snapshot = snapshot with { ComputedProps = snapshot.ComputedProps.Remove(key) };
                        if (TryResolve(callbacks.ComputedProps, callbacks.AllBindingIds, entry, callback, unresolved, diagnostics, out BehaviorComputedPropCallbackBinding? computedProp))
                        {
                            snapshot = snapshot with { ComputedProps = snapshot.ComputedProps.SetItem(key, new(computedProp!.Callback, bindingId)) };
                        }
                        break;
                    case BehaviorCatalogKind.Operation:
                        snapshot = snapshot with { Operations = snapshot.Operations.Remove(key) };
                        if (TryResolve(callbacks.Operations, callbacks.AllBindingIds, entry, callback, unresolved, diagnostics, out BehaviorOperationCallbackBinding? operation))
                        {
                            snapshot = snapshot with { Operations = snapshot.Operations.SetItem(key, new(operation!.Callback, bindingId)) };
                        }
                        break;
                    case BehaviorCatalogKind.Mutation:
                        snapshot = snapshot with { Mutations = snapshot.Mutations.Remove(key) };
                        if (TryResolve(callbacks.Mutations, callbacks.AllBindingIds, entry, callback, unresolved, diagnostics, out BehaviorMutationCallbackBinding? mutation))
                        {
                            snapshot = snapshot with { Mutations = snapshot.Mutations.SetItem(key, new(mutation!.Callback, bindingId)) };
                        }
                        break;
                    case BehaviorCatalogKind.Interceptor:
                        snapshot = RemoveInterceptor(snapshot, entry);
                        if (TryResolve(callbacks.Interceptors, callbacks.AllBindingIds, entry, callback, unresolved, diagnostics, out BehaviorInterceptorCallbackBinding? interceptor))
                        {
                            snapshot = AddInterceptor(snapshot, entry, interceptor!.Callback, bindingId!);
                        }
                        break;
                }
            }
        }

        return (snapshot, unresolved.ToImmutable(), diagnostics.ToImmutable());
    }

    private static CozoOmRegistrySnapshot StageConstraint(
        CozoOmRegistrySnapshot snapshot,
        BehaviorCatalogEntry entry,
        CallbackIndex callbacks,
        ImmutableArray<BehaviorUnresolvedDiagnostic>.Builder unresolved,
        ImmutableArray<BehaviorImportDiagnostic>.Builder diagnostics)
    {
        var key = (entry.OwnerClass, entry.Name);
        snapshot = snapshot with
        {
            Constraints = snapshot.Constraints.Remove(key),
            Validators = snapshot.Validators.Remove(key),
        };

        Func<OmValidationContext, ValueTask<bool>> when = MissingConstraint;
        Func<OmValidationContext, ValueTask<bool>> then = MissingConstraint;
        string? whenId = null;
        string? thenId = null;
        var hasConditionalSlot = false;
        foreach (var callback in entry.Callbacks)
        {
            if (callback.Slot == BehaviorCatalogCallbackSlot.Validator)
            {
                if (TryResolve(callbacks.Validators, callbacks.AllBindingIds, entry, callback, unresolved, diagnostics, out BehaviorValidatorCallbackBinding? validator))
                {
                    snapshot = snapshot with { Validators = snapshot.Validators.SetItem(key, new(validator!.Callback, callback.BindingId)) };
                }
                continue;
            }

            hasConditionalSlot = true;
            if (TryResolve(callbacks.Constraints, callbacks.AllBindingIds, entry, callback, unresolved, diagnostics, out BehaviorConstraintCallbackBinding? constraint))
            {
                if (callback.Slot == BehaviorCatalogCallbackSlot.When)
                {
                    when = constraint!.Callback;
                    whenId = callback.BindingId;
                }
                else
                {
                    then = constraint!.Callback;
                    thenId = callback.BindingId;
                }
            }
        }

        if (hasConditionalSlot)
        {
            snapshot = snapshot with { Constraints = snapshot.Constraints.SetItem(key, new(when, then, whenId, thenId)) };
        }

        return snapshot;
    }

    private static bool TryResolve<T>(
        IReadOnlyDictionary<string, T> typed,
        ISet<string> allBindingIds,
        BehaviorCatalogEntry entry,
        BehaviorCallbackBinding callback,
        ImmutableArray<BehaviorUnresolvedDiagnostic>.Builder unresolved,
        ImmutableArray<BehaviorImportDiagnostic>.Builder diagnostics,
        out T? binding)
        where T : class
    {
        binding = null;
        if (callback.BindingId is not { } bindingId)
        {
            diagnostics.Add(Diagnostic("OMI2001", entry, callback, null, "Unbound callback cannot be made ready by manifest data."));
            return false;
        }

        if (typed.TryGetValue(bindingId, out binding)) return true;
        var unresolvedDiagnostic = new BehaviorUnresolvedDiagnostic(
            "OMR1001",
            entry.Kind,
            entry.OwnerClass,
            BehaviorKey(entry),
            callback.Slot,
            bindingId,
            entry.InterceptorPhase,
            entry.InterceptorSeq);
        unresolved.Add(unresolvedDiagnostic);
        diagnostics.Add(Diagnostic(
            allBindingIds.Contains(bindingId) ? "OMI1202" : "OMI1201",
            entry,
            callback,
            bindingId,
            allBindingIds.Contains(bindingId)
                ? $"Binding '{bindingId}' has an incompatible delegate type."
                : $"Binding '{bindingId}' has no supplied typed callback."));
        return false;
    }

    private static async Task ApplyPersistentAsync(
        CozoOmRuntime runtime,
        IEnumerable<BehaviorCatalogEntry> entries,
        CancellationToken cancellationToken)
    {
        foreach (var entry in entries)
        {
            await PutMetadataAsync(runtime, entry, cancellationToken).ConfigureAwait(false);
            foreach (var key in AllBindingKeys(entry))
            {
                await BehaviorBindingLogic.RemoveAsync(runtime, key, cancellationToken).ConfigureAwait(false);
            }
            foreach (var callback in entry.Callbacks.Where(item => item.BindingId is not null))
            {
                await BehaviorBindingLogic.PutAsync(runtime, new(ToBindingKey(entry, callback.Slot), callback.BindingId!), cancellationToken).ConfigureAwait(false);
            }
        }
    }

    private static async Task RestorePersistentAsync(
        CozoOmRuntime runtime,
        IEnumerable<BehaviorCatalogEntry> imported,
        IEnumerable<BehaviorCatalogEntry> previous,
        IEnumerable<BehaviorBindingRow> previousBindings,
        CancellationToken cancellationToken)
    {
        foreach (var entry in imported)
        {
            await RemoveMetadataAsync(runtime, entry, cancellationToken).ConfigureAwait(false);
            foreach (var key in AllBindingKeys(entry))
            {
                await BehaviorBindingLogic.RemoveAsync(runtime, key, cancellationToken).ConfigureAwait(false);
            }
        }
        await ApplyPersistentAsync(runtime, previous, cancellationToken).ConfigureAwait(false);
        foreach (var row in previousBindings)
        {
            await BehaviorBindingLogic.PutAsync(runtime, row, cancellationToken).ConfigureAwait(false);
        }
    }

    private static Task PutMetadataAsync(CozoOmRuntime runtime, BehaviorCatalogEntry entry, CancellationToken cancellationToken) =>
        entry.Kind switch
        {
            BehaviorCatalogKind.Constraint => runtime.Store.RunAsync(
                CozoScriptBuilder.InputPut("om_constraint_def", ["class_name", "constraint_name"], ["constraint_kind", "message"]),
                LogicSupport.Params(("class_name", entry.OwnerClass), ("constraint_name", entry.Name), ("constraint_kind", entry.ConstraintKind ?? ""), ("message", entry.Message ?? "")),
                cancellationToken: cancellationToken),
            BehaviorCatalogKind.ComputedProp => PutDescriptionAsync(runtime, "om_computed_prop_def", "computed_prop_name", entry, cancellationToken),
            BehaviorCatalogKind.Operation => PutDescriptionAsync(runtime, "om_operation_def", "operation_name", entry, cancellationToken),
            BehaviorCatalogKind.Mutation => PutDescriptionAsync(runtime, "om_mutation_def", "mutation_name", entry, cancellationToken),
            BehaviorCatalogKind.Interceptor => runtime.Store.RunAsync(
                CozoScriptBuilder.InputPut("om_interceptor_def", ["class_name", "operation_name", "phase", "seq"], ["description"]),
                LogicSupport.Params(("class_name", entry.OwnerClass), ("operation_name", entry.Name), ("phase", entry.InterceptorPhase), ("seq", entry.InterceptorSeq), ("description", entry.Description ?? "")),
                cancellationToken: cancellationToken),
            _ => throw new ArgumentOutOfRangeException(nameof(entry.Kind)),
        };

    private static Task PutDescriptionAsync(CozoOmRuntime runtime, string relation, string nameColumn, BehaviorCatalogEntry entry, CancellationToken cancellationToken) =>
        runtime.Store.RunAsync(
            CozoScriptBuilder.InputPut(relation, [relation is "om_computed_prop_def" or "om_operation_def" ? "class_name" : "class_name", nameColumn], ["description"]),
            LogicSupport.Params((relation is "om_computed_prop_def" or "om_operation_def" ? "class_name" : "class_name", entry.OwnerClass), (nameColumn, entry.Name), ("description", entry.Description ?? "")),
            cancellationToken: cancellationToken);

    private static Task RemoveMetadataAsync(CozoOmRuntime runtime, BehaviorCatalogEntry entry, CancellationToken cancellationToken)
    {
        var (relation, keys) = entry.Kind switch
        {
            BehaviorCatalogKind.Constraint => ("om_constraint_def", new[] { "class_name", "constraint_name" }),
            BehaviorCatalogKind.ComputedProp => ("om_computed_prop_def", new[] { "class_name", "computed_prop_name" }),
            BehaviorCatalogKind.Operation => ("om_operation_def", new[] { "class_name", "operation_name" }),
            BehaviorCatalogKind.Mutation => ("om_mutation_def", new[] { "class_name", "mutation_name" }),
            BehaviorCatalogKind.Interceptor => ("om_interceptor_def", new[] { "class_name", "operation_name", "phase", "seq" }),
            _ => throw new ArgumentOutOfRangeException(nameof(entry.Kind)),
        };
        var values = entry.Kind switch
        {
            BehaviorCatalogKind.Constraint => new object?[] { entry.OwnerClass, entry.Name },
            BehaviorCatalogKind.ComputedProp => [entry.OwnerClass, entry.Name],
            BehaviorCatalogKind.Operation => [entry.OwnerClass, entry.Name],
            BehaviorCatalogKind.Mutation => [entry.OwnerClass, entry.Name],
            BehaviorCatalogKind.Interceptor => [entry.OwnerClass, entry.Name, entry.InterceptorPhase, entry.InterceptorSeq],
            _ => [],
        };
        var parameters = keys.Select((key, index) => (key, values[index])).ToArray();
        var head = string.Join(", ", keys);
        var input = string.Join(", ", keys.Select(key => "$" + key));
        return runtime.Store.RunAsync(
            $"?[{head}] <- [[{input}]]\n:rm {relation} {{{head}}}",
            LogicSupport.Params(parameters),
            cancellationToken: cancellationToken);
    }

    private static IEnumerable<BehaviorBindingKey> AllBindingKeys(BehaviorCatalogEntry entry)
    {
        var slots = entry.Kind switch
        {
            BehaviorCatalogKind.Constraint => new[] { BehaviorCatalogCallbackSlot.When, BehaviorCatalogCallbackSlot.Then, BehaviorCatalogCallbackSlot.Validator },
            BehaviorCatalogKind.ComputedProp => [BehaviorCatalogCallbackSlot.Compute],
            BehaviorCatalogKind.Operation => [BehaviorCatalogCallbackSlot.Handler],
            BehaviorCatalogKind.Mutation => [BehaviorCatalogCallbackSlot.Executor],
            BehaviorCatalogKind.Interceptor => [BehaviorCatalogCallbackSlot.Handler],
            _ => [],
        };
        return slots.Select(slot => ToBindingKey(entry, slot));
    }

    private static BehaviorBindingKey ToBindingKey(BehaviorCatalogEntry entry, BehaviorCatalogCallbackSlot slot) =>
        new(
            ToInternalKind(entry.Kind),
            entry.OwnerClass,
            entry.Name,
            ToInternalSlot(slot),
            entry.InterceptorPhase ?? BehaviorBindingLogic.NonInterceptorPhase,
            entry.InterceptorSeq ?? BehaviorBindingLogic.NonInterceptorSeq);

    private static CozoOmRegistrySnapshot RemoveInterceptor(CozoOmRegistrySnapshot snapshot, BehaviorCatalogEntry entry)
    {
        var source = entry.InterceptorPhase == "before" ? snapshot.BeforeInterceptors : snapshot.AfterInterceptors;
        var key = (entry.OwnerClass, entry.Name);
        if (source.TryGetValue(key, out var list))
        {
            var next = list.RemoveAll(item => item.Seq == entry.InterceptorSeq);
            source = next.IsEmpty ? source.Remove(key) : source.SetItem(key, next);
        }
        return entry.InterceptorPhase == "before" ? snapshot with { BeforeInterceptors = source } : snapshot with { AfterInterceptors = source };
    }

    private static CozoOmRegistrySnapshot AddInterceptor(
        CozoOmRegistrySnapshot snapshot,
        BehaviorCatalogEntry entry,
        Func<OmOperationContext, ValueTask> callback,
        string bindingId)
    {
        var source = entry.InterceptorPhase == "before" ? snapshot.BeforeInterceptors : snapshot.AfterInterceptors;
        var key = (entry.OwnerClass, entry.Name);
        var list = source.TryGetValue(key, out var existing) ? existing : ImmutableArray<OmInterceptorRegistration>.Empty;
        list = list.Add(new(callback, entry.InterceptorSeq!.Value, entry.Description ?? "", entry.OwnerClass, bindingId));
        source = source.SetItem(key, list.OrderBy(item => item.Seq).ToImmutableArray());
        return entry.InterceptorPhase == "before" ? snapshot with { BeforeInterceptors = source } : snapshot with { AfterInterceptors = source };
    }

    private static BehaviorImportDiagnostic Diagnostic(
        string code,
        BehaviorCatalogEntry entry,
        BehaviorCallbackBinding? callback,
        string? bindingId,
        string message) =>
        new(code, BehaviorKey(entry), message, entry.Kind, entry.OwnerClass, entry.Name, callback?.Slot, bindingId ?? callback?.BindingId, entry.InterceptorPhase, entry.InterceptorSeq);

    private static string BehaviorKey(BehaviorCatalogEntry entry) =>
        entry.Kind == BehaviorCatalogKind.Interceptor
            ? $"interceptor:{entry.OwnerClass}/{entry.Name}/{entry.InterceptorPhase}/{entry.InterceptorSeq}"
            : $"{KindWire(entry.Kind)}:{entry.OwnerClass}/{entry.Name}";

    private static string KindWire(BehaviorCatalogKind kind) => kind switch
    {
        BehaviorCatalogKind.Constraint => "constraint",
        BehaviorCatalogKind.ComputedProp => "computedProp",
        BehaviorCatalogKind.Operation => "operation",
        BehaviorCatalogKind.Mutation => "mutation",
        BehaviorCatalogKind.Interceptor => "interceptor",
        _ => throw new ArgumentOutOfRangeException(nameof(kind)),
    };

    private static bool SameBehaviorKey(BehaviorCatalogEntry left, BehaviorCatalogEntry right) =>
        left.Kind == right.Kind && left.OwnerClass == right.OwnerClass && left.Name == right.Name
        && left.InterceptorPhase == right.InterceptorPhase && left.InterceptorSeq == right.InterceptorSeq;

    private static bool SameBehaviorKey(BehaviorBindingKey key, BehaviorCatalogEntry entry) =>
        ToCatalogKind(key.BehaviorKind) == entry.Kind && key.OwnerClass == entry.OwnerClass && key.BehaviorName == entry.Name
        && (entry.Kind != BehaviorCatalogKind.Interceptor || key.Phase == entry.InterceptorPhase && key.Seq == entry.InterceptorSeq);

    private static BehaviorKind ToInternalKind(BehaviorCatalogKind kind) => kind switch
    {
        BehaviorCatalogKind.Constraint => BehaviorKind.Constraint,
        BehaviorCatalogKind.ComputedProp => BehaviorKind.ComputedProp,
        BehaviorCatalogKind.Operation => BehaviorKind.Operation,
        BehaviorCatalogKind.Mutation => BehaviorKind.Mutation,
        BehaviorCatalogKind.Interceptor => BehaviorKind.Interceptor,
        _ => throw new ArgumentOutOfRangeException(nameof(kind)),
    };

    private static BehaviorCatalogKind ToCatalogKind(BehaviorKind kind) => kind switch
    {
        BehaviorKind.Constraint => BehaviorCatalogKind.Constraint,
        BehaviorKind.ComputedProp => BehaviorCatalogKind.ComputedProp,
        BehaviorKind.Operation => BehaviorCatalogKind.Operation,
        BehaviorKind.Mutation => BehaviorCatalogKind.Mutation,
        BehaviorKind.Interceptor => BehaviorCatalogKind.Interceptor,
        _ => throw new ArgumentOutOfRangeException(nameof(kind)),
    };

    private static BehaviorCallbackSlot ToInternalSlot(BehaviorCatalogCallbackSlot slot) => slot switch
    {
        BehaviorCatalogCallbackSlot.When => BehaviorCallbackSlot.When,
        BehaviorCatalogCallbackSlot.Then => BehaviorCallbackSlot.Then,
        BehaviorCatalogCallbackSlot.Validator => BehaviorCallbackSlot.Validator,
        BehaviorCatalogCallbackSlot.Compute => BehaviorCallbackSlot.Compute,
        BehaviorCatalogCallbackSlot.Handler => BehaviorCallbackSlot.Handler,
        BehaviorCatalogCallbackSlot.Executor => BehaviorCallbackSlot.Executor,
        _ => throw new ArgumentOutOfRangeException(nameof(slot)),
    };

    private sealed class CallbackIndex
    {
        private CallbackIndex() { }

        internal Dictionary<string, BehaviorConstraintCallbackBinding> Constraints { get; } = new(StringComparer.Ordinal);
        internal Dictionary<string, BehaviorValidatorCallbackBinding> Validators { get; } = new(StringComparer.Ordinal);
        internal Dictionary<string, BehaviorComputedPropCallbackBinding> ComputedProps { get; } = new(StringComparer.Ordinal);
        internal Dictionary<string, BehaviorOperationCallbackBinding> Operations { get; } = new(StringComparer.Ordinal);
        internal Dictionary<string, BehaviorMutationCallbackBinding> Mutations { get; } = new(StringComparer.Ordinal);
        internal Dictionary<string, BehaviorInterceptorCallbackBinding> Interceptors { get; } = new(StringComparer.Ordinal);
        internal HashSet<string> AllBindingIds { get; } = new(StringComparer.Ordinal);
        internal ImmutableArray<BehaviorImportDiagnostic> Diagnostics { get; private set; }

        internal static CallbackIndex Create(BehaviorCallbackBindingSet set)
        {
            var index = new CallbackIndex();
            var diagnostics = ImmutableArray.CreateBuilder<BehaviorImportDiagnostic>();
            Add(set.Constraints, index.Constraints, item => item.BindingId, item => item.Callback, diagnostics);
            Add(set.Validators, index.Validators, item => item.BindingId, item => item.Callback, diagnostics);
            Add(set.ComputedProps, index.ComputedProps, item => item.BindingId, item => item.Callback, diagnostics);
            Add(set.Operations, index.Operations, item => item.BindingId, item => item.Callback, diagnostics);
            Add(set.Mutations, index.Mutations, item => item.BindingId, item => item.Callback, diagnostics);
            Add(set.Interceptors, index.Interceptors, item => item.BindingId, item => item.Callback, diagnostics);
            foreach (var id in index.Constraints.Keys.Concat(index.Validators.Keys).Concat(index.ComputedProps.Keys)
                         .Concat(index.Operations.Keys).Concat(index.Mutations.Keys).Concat(index.Interceptors.Keys))
            {
                index.AllBindingIds.Add(id);
            }
            index.Diagnostics = diagnostics.ToImmutable();
            return index;
        }

        private static void Add<T, TDelegate>(
            IEnumerable<T> source,
            IDictionary<string, T> target,
            Func<T, string> getId,
            Func<T, TDelegate> getDelegate,
            ImmutableArray<BehaviorImportDiagnostic>.Builder diagnostics)
            where T : class
            where TDelegate : Delegate
        {
            foreach (var item in source)
            {
                var id = getId(item);
                if (string.IsNullOrWhiteSpace(id) || getDelegate(item) is null)
                {
                    diagnostics.Add(new("OMI1001", "$callbacks", "Typed callback binding requires a non-empty id and delegate.", BindingId: id));
                }
                else if (!target.TryAdd(id, item))
                {
                    diagnostics.Add(new("OMI1002", "$callbacks", $"Typed callback binding '{id}' is duplicated for the same delegate type.", BindingId: id));
                }
            }
        }
    }
}
