using System.Collections.Immutable;
using System.Text.Json;
using Depa.Ontology.Contracts;
using Depa.Ontology.Contracts.Models;
using Depa.Ontology.Inputs;
using Depa.Ontology.Logic;

namespace Depa.Ontology.Runtime;

public sealed record CozoOmRuntime(
    ICozoOmStore Store,
    CozoOmOptions Options,
    CozoOmRegistry Registry)
{
    internal BehaviorRuntimeGate BehaviorGate { get; init; } = new();
    internal BehaviorImportFaultHooks BehaviorImportFaults { get; init; } = new();
}

public sealed record CozoOmOptions
{
    public int DefaultMaxChaseIterations { get; init; } = 10;
    public TimeProvider TimeProvider { get; init; } = TimeProvider.System;
}

public sealed class CozoOmRegistry
{
    private readonly object _writeLock = new();
    private CozoOmRegistrySnapshot _snapshot = CozoOmRegistrySnapshot.Empty;

    internal CozoOmRegistrySnapshot CaptureSnapshot() => Volatile.Read(ref _snapshot);

    internal void Clear() => Update(static _ => CozoOmRegistrySnapshot.Empty);

    public void RegisterValidator(string className, string constraintName, Func<OmValidationContext, ValueTask<string?>> validator)
        => RegisterValidatorCore(className, constraintName, validator, null);

    public void RegisterValidator(
        string className,
        string constraintName,
        string bindingId,
        Func<OmValidationContext, ValueTask<string?>> validator)
        => RegisterValidatorCore(className, constraintName, validator, RequireBindingId(bindingId));

    private void RegisterValidatorCore(
        string className,
        string constraintName,
        Func<OmValidationContext, ValueTask<string?>> validator,
        string? bindingId)
    {
        ArgumentNullException.ThrowIfNull(validator);
        Update(snapshot => snapshot with
        {
            Validators = snapshot.Validators.SetItem(
                (className, constraintName),
                new OmCallbackRegistration<Func<OmValidationContext, ValueTask<string?>>>(validator, bindingId)),
        });
    }

    public bool TryGetValidator(string className, string constraintName, out Func<OmValidationContext, ValueTask<string?>> validator)
    {
        if (CaptureSnapshot().Validators.TryGetValue((className, constraintName), out var registration))
        {
            validator = registration.Callback;
            return true;
        }

        validator = null!;
        return false;
    }

    public void RegisterConstraint(
        string className,
        string constraintName,
        Func<OmValidationContext, ValueTask<bool>> when,
        Func<OmValidationContext, ValueTask<bool>> then)
        => RegisterConstraintCore(className, constraintName, when, null, then, null);

    public void RegisterConstraint(
        string className,
        string constraintName,
        string whenBindingId,
        Func<OmValidationContext, ValueTask<bool>> when,
        string thenBindingId,
        Func<OmValidationContext, ValueTask<bool>> then)
        => RegisterConstraintCore(
            className,
            constraintName,
            when,
            RequireBindingId(whenBindingId),
            then,
            RequireBindingId(thenBindingId));

    private void RegisterConstraintCore(
        string className,
        string constraintName,
        Func<OmValidationContext, ValueTask<bool>> when,
        string? whenBindingId,
        Func<OmValidationContext, ValueTask<bool>> then,
        string? thenBindingId)
    {
        var registration = new OmConstraintRegistration(
            when ?? throw new ArgumentNullException(nameof(when)),
            then ?? throw new ArgumentNullException(nameof(then)),
            whenBindingId,
            thenBindingId);
        Update(snapshot => snapshot with
        {
            Constraints = snapshot.Constraints.SetItem((className, constraintName), registration),
        });
    }

    public bool TryGetConstraint(string className, string constraintName, out OmConstraintRegistration constraint)
    {
        return CaptureSnapshot().Constraints.TryGetValue((className, constraintName), out constraint!);
    }

    internal void RestoreConstraintRegistration(
        string className,
        string constraintName,
        OmConstraintRegistration registration) =>
        RegisterConstraintCore(
            className,
            constraintName,
            registration.When,
            registration.WhenBindingId,
            registration.Then,
            registration.ThenBindingId);

    internal void UnregisterConstraint(string className, string constraintName)
    {
        Update(snapshot => snapshot with
        {
            Constraints = snapshot.Constraints.Remove((className, constraintName)),
        });
    }

    public void RegisterComputedProp(string className, string computedPropName, Func<OmComputedPropContext, ValueTask<object?>> compute)
        => RegisterComputedPropCore(className, computedPropName, compute, null);

    public void RegisterComputedProp(
        string className,
        string computedPropName,
        string bindingId,
        Func<OmComputedPropContext, ValueTask<object?>> compute)
        => RegisterComputedPropCore(className, computedPropName, compute, RequireBindingId(bindingId));

    private void RegisterComputedPropCore(
        string className,
        string computedPropName,
        Func<OmComputedPropContext, ValueTask<object?>> compute,
        string? bindingId)
    {
        ArgumentNullException.ThrowIfNull(compute);
        Update(snapshot => snapshot with
        {
            ComputedProps = snapshot.ComputedProps.SetItem(
                (className, computedPropName),
                new OmCallbackRegistration<Func<OmComputedPropContext, ValueTask<object?>>>(compute, bindingId)),
        });
    }

    public bool TryGetComputedProp(string className, string computedPropName, out Func<OmComputedPropContext, ValueTask<object?>> compute)
    {
        if (CaptureSnapshot().ComputedProps.TryGetValue((className, computedPropName), out var registration))
        {
            compute = registration.Callback;
            return true;
        }

        compute = null!;
        return false;
    }

    public void RegisterMutation(string className, string mutationName, Func<OmMutationContext, IReadOnlyDictionary<string, object?>, ValueTask> executor)
        => RegisterMutationCore(className, mutationName, executor, null);

    public void RegisterMutation(
        string className,
        string mutationName,
        string bindingId,
        Func<OmMutationContext, IReadOnlyDictionary<string, object?>, ValueTask> executor)
        => RegisterMutationCore(className, mutationName, executor, RequireBindingId(bindingId));

    private void RegisterMutationCore(
        string className,
        string mutationName,
        Func<OmMutationContext, IReadOnlyDictionary<string, object?>, ValueTask> executor,
        string? bindingId)
    {
        ArgumentNullException.ThrowIfNull(executor);
        Update(snapshot => snapshot with
        {
            Mutations = snapshot.Mutations.SetItem(
                (className, mutationName),
                new OmCallbackRegistration<Func<OmMutationContext, IReadOnlyDictionary<string, object?>, ValueTask>>(executor, bindingId)),
        });
    }

    public bool TryGetMutation(string className, string mutationName, out Func<OmMutationContext, IReadOnlyDictionary<string, object?>, ValueTask> executor)
    {
        if (CaptureSnapshot().Mutations.TryGetValue((className, mutationName), out var registration))
        {
            executor = registration.Callback;
            return true;
        }

        executor = null!;
        return false;
    }

    internal bool TryGetMutationRegistration(
        string className,
        string mutationName,
        out OmCallbackRegistration<Func<OmMutationContext, IReadOnlyDictionary<string, object?>, ValueTask>> registration) =>
        CaptureSnapshot().Mutations.TryGetValue((className, mutationName), out registration!);

    internal void RestoreMutationRegistration(
        string className,
        string mutationName,
        OmCallbackRegistration<Func<OmMutationContext, IReadOnlyDictionary<string, object?>, ValueTask>> registration) =>
        RegisterMutationCore(className, mutationName, registration.Callback, registration.BindingId);

    internal void UnregisterMutation(string className, string mutationName)
    {
        Update(snapshot => snapshot with
        {
            Mutations = snapshot.Mutations.Remove((className, mutationName)),
        });
    }

    public void RegisterOperation(
        string className,
        string operationName,
        Func<OmOperationContext, IReadOnlyDictionary<string, object?>, ValueTask<IReadOnlyList<MutationSpec>>> handler)
        => RegisterOperationCore(className, operationName, handler, null);

    public void RegisterOperation(
        string className,
        string operationName,
        string bindingId,
        Func<OmOperationContext, IReadOnlyDictionary<string, object?>, ValueTask<IReadOnlyList<MutationSpec>>> handler)
        => RegisterOperationCore(className, operationName, handler, RequireBindingId(bindingId));

    private void RegisterOperationCore(
        string className,
        string operationName,
        Func<OmOperationContext, IReadOnlyDictionary<string, object?>, ValueTask<IReadOnlyList<MutationSpec>>> handler,
        string? bindingId)
    {
        ArgumentNullException.ThrowIfNull(handler);
        Update(snapshot => snapshot with
        {
            Operations = snapshot.Operations.SetItem(
                (className, operationName),
                new OmCallbackRegistration<Func<OmOperationContext, IReadOnlyDictionary<string, object?>, ValueTask<IReadOnlyList<MutationSpec>>>>(handler, bindingId)),
        });
    }

    public bool TryGetOperation(
        string className,
        string operationName,
        out Func<OmOperationContext, IReadOnlyDictionary<string, object?>, ValueTask<IReadOnlyList<MutationSpec>>> handler)
    {
        if (CaptureSnapshot().Operations.TryGetValue((className, operationName), out var registration))
        {
            handler = registration.Callback;
            return true;
        }

        handler = null!;
        return false;
    }

    internal bool TryGetOperationRegistration(
        string className,
        string operationName,
        out OmCallbackRegistration<Func<OmOperationContext, IReadOnlyDictionary<string, object?>, ValueTask<IReadOnlyList<MutationSpec>>>> registration) =>
        CaptureSnapshot().Operations.TryGetValue((className, operationName), out registration!);

    internal void RestoreOperationRegistration(
        string className,
        string operationName,
        OmCallbackRegistration<Func<OmOperationContext, IReadOnlyDictionary<string, object?>, ValueTask<IReadOnlyList<MutationSpec>>>> registration) =>
        RegisterOperationCore(className, operationName, registration.Callback, registration.BindingId);

    internal void UnregisterOperation(string className, string operationName)
    {
        Update(snapshot => snapshot with
        {
            Operations = snapshot.Operations.Remove((className, operationName)),
        });
    }

    public int RegisterInterceptor(
        string className,
        string operationName,
        string phase,
        Func<OmOperationContext, ValueTask> handler,
        string description = "")
    {
        var normalizedPhase = NormalizePhase(phase);
        ArgumentNullException.ThrowIfNull(handler);
        lock (_writeLock)
        {
            var snapshot = _snapshot;
            var target = InterceptorTarget(snapshot, normalizedPhase);
            var key = (className, operationName);
            var list = target.TryGetValue(key, out var existing) ? existing : [];
            var seq = list.IsDefaultOrEmpty ? 0 : list.Max(item => item.Seq) + 1;
            var next = list.Add(new OmInterceptorRegistration(handler, seq, description, className));
            Publish(SetInterceptorTarget(snapshot, normalizedPhase, target.SetItem(key, next)));
            return seq;
        }
    }

    public void RegisterInterceptor(
        string className,
        string operationName,
        string phase,
        int seq,
        string bindingId,
        Func<OmOperationContext, ValueTask> handler,
        string description = "")
        => RegisterInterceptorCore(
            className,
            operationName,
            phase,
            seq,
            handler,
            description,
            RequireBindingId(bindingId));

    internal int NextInterceptorSeq(string className, string operationName, string phase)
    {
        var target = InterceptorTarget(CaptureSnapshot(), NormalizePhase(phase));
        return target.TryGetValue((className, operationName), out var list) && !list.IsDefaultOrEmpty
            ? list.Max(item => item.Seq) + 1
            : 0;
    }

    internal void RegisterInterceptor(
        string className,
        string operationName,
        string phase,
        int seq,
        Func<OmOperationContext, ValueTask> handler,
        string description = "")
        => RegisterInterceptorCore(className, operationName, phase, seq, handler, description, null);

    private void RegisterInterceptorCore(
        string className,
        string operationName,
        string phase,
        int seq,
        Func<OmOperationContext, ValueTask> handler,
        string description,
        string? bindingId)
    {
        var normalizedPhase = NormalizePhase(phase);
        ArgumentNullException.ThrowIfNull(handler);
        lock (_writeLock)
        {
            var snapshot = _snapshot;
            var target = InterceptorTarget(snapshot, normalizedPhase);
            var key = (className, operationName);
            var list = target.TryGetValue(key, out var existing) ? existing : [];
            var registration = new OmInterceptorRegistration(handler, seq, description, className, bindingId);
            var existingIndex = -1;
            for (var index = 0; index < list.Length; index++)
            {
                if (list[index].Seq == seq)
                {
                    existingIndex = index;
                    break;
                }
            }
            var next = existingIndex >= 0 ? list.SetItem(existingIndex, registration) : list.Add(registration);
            Publish(SetInterceptorTarget(snapshot, normalizedPhase, target.SetItem(key, next)));
        }
    }

    internal bool TryGetInterceptor(string className, string operationName, string phase, int seq, out OmInterceptorRegistration registration)
    {
        var target = InterceptorTarget(CaptureSnapshot(), NormalizePhase(phase));
        if (target.TryGetValue((className, operationName), out var list))
        {
            foreach (var item in list)
            {
                if (item.Seq == seq)
                {
                    registration = item;
                    return true;
                }
            }
        }

        registration = null!;
        return false;
    }

    internal void RestoreInterceptorRegistration(
        string className,
        string operationName,
        string phase,
        OmInterceptorRegistration registration) =>
        RegisterInterceptorCore(
            className,
            operationName,
            phase,
            registration.Seq,
            registration.Handler,
            registration.Description,
            registration.BindingId);

    internal void UnregisterInterceptor(string className, string operationName, string phase, int seq)
    {
        var normalizedPhase = NormalizePhase(phase);
        lock (_writeLock)
        {
            var snapshot = _snapshot;
            var target = InterceptorTarget(snapshot, normalizedPhase);
            var key = (className, operationName);
            if (!target.TryGetValue(key, out var list)) return;
            var next = list.RemoveAll(item => item.Seq == seq);
            target = next.IsEmpty ? target.Remove(key) : target.SetItem(key, next);
            Publish(SetInterceptorTarget(snapshot, normalizedPhase, target));
        }
    }

    public IReadOnlyList<OmInterceptorRegistration> GetInterceptors(string className, string operationName, string phase)
    {
        var target = InterceptorTarget(CaptureSnapshot(), NormalizePhase(phase));
        return target.TryGetValue((className, operationName), out var list)
            ? list.OrderBy(item => item.Seq).ToImmutableArray()
            : ImmutableArray<OmInterceptorRegistration>.Empty;
    }

    private void Update(Func<CozoOmRegistrySnapshot, CozoOmRegistrySnapshot> update)
    {
        lock (_writeLock)
        {
            Publish(update(_snapshot));
        }
    }

    private void Publish(CozoOmRegistrySnapshot snapshot) => Volatile.Write(ref _snapshot, snapshot);

    internal bool TryPublishSnapshot(
        CozoOmRegistrySnapshot expected,
        CozoOmRegistrySnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(expected);
        ArgumentNullException.ThrowIfNull(snapshot);
        lock (_writeLock)
        {
            if (!ReferenceEquals(_snapshot, expected))
            {
                return false;
            }

            Publish(snapshot);
            return true;
        }
    }

    private static string RequireBindingId(string bindingId) =>
        !string.IsNullOrWhiteSpace(bindingId)
            ? bindingId
            : throw new ArgumentException("Binding id must be non-empty", nameof(bindingId));

    private static string NormalizePhase(string phase) => phase.Trim().ToLowerInvariant() switch
    {
        "before" => "before",
        "after" => "after",
        _ => throw new ArgumentException("Interceptor phase must be 'before' or 'after'", nameof(phase)),
    };

    private static ImmutableDictionary<(string ClassName, string OperationName), ImmutableArray<OmInterceptorRegistration>> InterceptorTarget(
        CozoOmRegistrySnapshot snapshot,
        string phase) => phase == "before" ? snapshot.BeforeInterceptors : snapshot.AfterInterceptors;

    private static CozoOmRegistrySnapshot SetInterceptorTarget(
        CozoOmRegistrySnapshot snapshot,
        string phase,
        ImmutableDictionary<(string ClassName, string OperationName), ImmutableArray<OmInterceptorRegistration>> target) =>
        phase == "before"
            ? snapshot with { BeforeInterceptors = target }
            : snapshot with { AfterInterceptors = target };
}

internal sealed class BehaviorRuntimeGate
{
    private readonly SemaphoreSlim _semaphore = new(1, 1);
    private int _waitingCount;

    internal int WaitingCount => Volatile.Read(ref _waitingCount);

    internal async ValueTask<BehaviorRuntimeGateLease> EnterAsync(CancellationToken cancellationToken = default)
    {
        Interlocked.Increment(ref _waitingCount);
        try
        {
            await _semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            Interlocked.Decrement(ref _waitingCount);
        }

        return new BehaviorRuntimeGateLease(_semaphore);
    }

    internal async Task<T> ResolveAsync<T>(
        CozoOmRuntime runtime,
        Func<BehaviorResolutionScope, CancellationToken, Task<T>> resolver,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(runtime);
        ArgumentNullException.ThrowIfNull(resolver);
        await using var lease = await EnterAsync(cancellationToken).ConfigureAwait(false);
        var scope = new BehaviorResolutionScope(runtime.Registry.CaptureSnapshot());
        return await resolver(scope, cancellationToken).ConfigureAwait(false);
    }

    internal async Task<T> ResolveAsync<T>(
        BehaviorResolutionScope scope,
        Func<BehaviorResolutionScope, CancellationToken, Task<T>> resolver,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(scope);
        ArgumentNullException.ThrowIfNull(resolver);
        await using var lease = await EnterAsync(cancellationToken).ConfigureAwait(false);
        return await resolver(scope, cancellationToken).ConfigureAwait(false);
    }
}

internal sealed class BehaviorRuntimeGateLease(SemaphoreSlim semaphore) : IAsyncDisposable, IDisposable
{
    private SemaphoreSlim? _semaphore = semaphore;

    public void Dispose() => Interlocked.Exchange(ref _semaphore, null)?.Release();

    internal void PublishRegistrySnapshot(
        CozoOmRegistry registry,
        CozoOmRegistrySnapshot expected,
        CozoOmRegistrySnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(expected);
        ArgumentNullException.ThrowIfNull(snapshot);
        if (Volatile.Read(ref _semaphore) is null)
        {
            throw new ObjectDisposedException(nameof(BehaviorRuntimeGateLease));
        }

        if (!registry.TryPublishSnapshot(expected, snapshot))
        {
            throw new BehaviorRegistryPublicationConflictException();
        }
    }

    public ValueTask DisposeAsync()
    {
        Dispose();
        return ValueTask.CompletedTask;
    }
}

internal sealed class BehaviorRegistryPublicationConflictException()
    : InvalidOperationException("Behavior registry changed after import staging; staged publication was rejected.");

internal sealed class BehaviorImportFaultHooks
{
    internal Func<CancellationToken, Task>? BeforePersistentCommitAsync { get; set; }
    internal Func<Task>? AfterPersistentCommitAsync { get; set; }
    internal Action? BeforeRegistryPublish { get; set; }
    internal Func<Task>? BeforePersistentCompensationAsync { get; set; }

    internal void Reset()
    {
        BeforePersistentCommitAsync = null;
        AfterPersistentCommitAsync = null;
        BeforeRegistryPublish = null;
        BeforePersistentCompensationAsync = null;
    }
}

internal sealed record BehaviorResolutionScope(CozoOmRegistrySnapshot RegistrySnapshot);

internal sealed record OmCallbackRegistration<TDelegate>(TDelegate Callback, string? BindingId)
    where TDelegate : Delegate;

internal sealed record CozoOmRegistrySnapshot(
    ImmutableDictionary<(string ClassName, string ConstraintName), OmCallbackRegistration<Func<OmValidationContext, ValueTask<string?>>>> Validators,
    ImmutableDictionary<(string ClassName, string ConstraintName), OmConstraintRegistration> Constraints,
    ImmutableDictionary<(string ClassName, string ComputedPropName), OmCallbackRegistration<Func<OmComputedPropContext, ValueTask<object?>>>> ComputedProps,
    ImmutableDictionary<(string ClassName, string MutationName), OmCallbackRegistration<Func<OmMutationContext, IReadOnlyDictionary<string, object?>, ValueTask>>> Mutations,
    ImmutableDictionary<(string ClassName, string OperationName), OmCallbackRegistration<Func<OmOperationContext, IReadOnlyDictionary<string, object?>, ValueTask<IReadOnlyList<MutationSpec>>>>> Operations,
    ImmutableDictionary<(string ClassName, string OperationName), ImmutableArray<OmInterceptorRegistration>> BeforeInterceptors,
    ImmutableDictionary<(string ClassName, string OperationName), ImmutableArray<OmInterceptorRegistration>> AfterInterceptors)
{
    internal static CozoOmRegistrySnapshot Empty { get; } = new(
        ImmutableDictionary<(string, string), OmCallbackRegistration<Func<OmValidationContext, ValueTask<string?>>>>.Empty,
        ImmutableDictionary<(string, string), OmConstraintRegistration>.Empty,
        ImmutableDictionary<(string, string), OmCallbackRegistration<Func<OmComputedPropContext, ValueTask<object?>>>>.Empty,
        ImmutableDictionary<(string, string), OmCallbackRegistration<Func<OmMutationContext, IReadOnlyDictionary<string, object?>, ValueTask>>>.Empty,
        ImmutableDictionary<(string, string), OmCallbackRegistration<Func<OmOperationContext, IReadOnlyDictionary<string, object?>, ValueTask<IReadOnlyList<MutationSpec>>>>>.Empty,
        ImmutableDictionary<(string, string), ImmutableArray<OmInterceptorRegistration>>.Empty,
        ImmutableDictionary<(string, string), ImmutableArray<OmInterceptorRegistration>>.Empty);

    internal bool TryGetBindingId(BehaviorBindingKey key, out string? bindingId)
    {
        bindingId = key.BehaviorKind switch
        {
            BehaviorKind.Constraint when key.CallbackSlot == BehaviorCallbackSlot.Validator &&
                                             Validators.TryGetValue((key.OwnerClass, key.BehaviorName), out var validator) => validator.BindingId,
            BehaviorKind.Constraint when key.CallbackSlot == BehaviorCallbackSlot.When &&
                                             Constraints.TryGetValue((key.OwnerClass, key.BehaviorName), out var constraint) => constraint.WhenBindingId,
            BehaviorKind.Constraint when key.CallbackSlot == BehaviorCallbackSlot.Then &&
                                             Constraints.TryGetValue((key.OwnerClass, key.BehaviorName), out var constraint) => constraint.ThenBindingId,
            BehaviorKind.ComputedProp when ComputedProps.TryGetValue((key.OwnerClass, key.BehaviorName), out var computedProp) => computedProp.BindingId,
            BehaviorKind.Operation when Operations.TryGetValue((key.OwnerClass, key.BehaviorName), out var operation) => operation.BindingId,
            BehaviorKind.Mutation when Mutations.TryGetValue((key.OwnerClass, key.BehaviorName), out var mutation) => mutation.BindingId,
            BehaviorKind.Interceptor => GetInterceptorBindingId(key),
            _ => null,
        };
        return bindingId is not null;
    }

    private string? GetInterceptorBindingId(BehaviorBindingKey key)
    {
        var source = key.Phase == "before" ? BeforeInterceptors : AfterInterceptors;
        if (!source.TryGetValue((key.OwnerClass, key.BehaviorName), out var registrations)) return null;
        foreach (var registration in registrations)
        {
            if (registration.Seq == key.Seq) return registration.BindingId;
        }

        return null;
    }
}

public sealed record OmConstraintRegistration(
    Func<OmValidationContext, ValueTask<bool>> When,
    Func<OmValidationContext, ValueTask<bool>> Then,
    string? WhenBindingId = null,
    string? ThenBindingId = null);

public sealed record OmValidationContext(CozoOmRuntime Runtime, string ObjectId, string ClassName)
{
    public Task<JsonElement?> GetFieldValueAsync(string fieldName, CancellationToken cancellationToken = default) =>
        Logic.ObjectLogic.GetFieldValueAsync(Runtime, ObjectId, fieldName, cancellationToken);

    public Task<JsonElement?> GetFieldValueAsOfAsync(string fieldName, string asOf, CancellationToken cancellationToken = default) =>
        Logic.ObjectLogic.GetFieldValueAsOfAsync(Runtime, ObjectId, fieldName, asOf, cancellationToken);

    public Task<NeighborResult> GetNeighborsAsync(string? relationName = null, OmDirection direction = OmDirection.Both, CancellationToken cancellationToken = default) =>
        Logic.RelationLogic.GetNeighborsAsync(Runtime, ObjectId, relationName, direction, cancellationToken);
}

public sealed record OmComputedPropContext(CozoOmRuntime Runtime, string ObjectId, string ClassName, string? AsOf = null)
{
    public Task<JsonElement?> GetFieldValueAsync(string fieldName, CancellationToken cancellationToken = default) =>
        AsOf is null
            ? Logic.ObjectLogic.GetFieldValueAsync(Runtime, ObjectId, fieldName, cancellationToken)
            : Logic.ObjectLogic.GetFieldValueAsOfAsync(Runtime, ObjectId, fieldName, AsOf, cancellationToken);

    public Task<NeighborResult> GetNeighborsAsync(string? relationName = null, OmDirection direction = OmDirection.Both, CancellationToken cancellationToken = default) =>
        AsOf is null
            ? Logic.RelationLogic.GetNeighborsAsync(Runtime, ObjectId, relationName, direction, cancellationToken)
            : Logic.RelationLogic.GetNeighborsAsOfAsync(Runtime, ObjectId, relationName, AsOf, direction, cancellationToken);
}

public sealed record MutationSpec(string Mutation, IReadOnlyDictionary<string, object?>? Params = null);

public sealed record OmInterceptorRegistration(
    Func<OmOperationContext, ValueTask> Handler,
    int Seq,
    string Description,
    string OwnerClass,
    string? BindingId = null);

public record OmMutationContext(CozoOmRuntime Runtime, string ObjectId, string ClassName)
{
    public Task<JsonElement?> GetFieldValueAsync(string fieldName, CancellationToken cancellationToken = default) =>
        Logic.ObjectLogic.GetFieldValueAsync(Runtime, ObjectId, fieldName, cancellationToken);

    public Task<JsonElement?> GetFieldValueAsOfAsync(string fieldName, string asOf, CancellationToken cancellationToken = default) =>
        Logic.ObjectLogic.GetFieldValueAsOfAsync(Runtime, ObjectId, fieldName, asOf, cancellationToken);

    public Task SetFieldValueAsync(string fieldName, object? value, WriteOptions? options = null, CancellationToken cancellationToken = default) =>
        Logic.ObjectLogic.SetFieldValueAsync(Runtime, new SetFieldValueInput(ObjectId, fieldName, value, options), cancellationToken);

    public Task CreateRelationLinkAsync(string relationName, string toObjectId, object? payload = null, WriteOptions? options = null, CancellationToken cancellationToken = default) =>
        Logic.RelationLogic.CreateRelationLinkAsync(Runtime, new CreateRelationLinkInput(ObjectId, relationName, toObjectId, payload, options), cancellationToken);

    public Task<NeighborResult> GetNeighborsAsync(string? relationName = null, OmDirection direction = OmDirection.Both, CancellationToken cancellationToken = default) =>
        Logic.RelationLogic.GetNeighborsAsync(Runtime, ObjectId, relationName, direction, cancellationToken);
}

public sealed record OmOperationContext(
    CozoOmRuntime Runtime,
    string ObjectId,
    string ClassName,
    string OperationOwnerClass,
    IReadOnlyDictionary<string, object?> Params)
    : OmMutationContext(Runtime, ObjectId, ClassName)
{
    internal BehaviorResolutionScope? BehaviorResolution { get; init; }

    public Task<IReadOnlyList<MutationSpec>> CallParentOperationAsync(
        string operationName,
        IReadOnlyDictionary<string, object?>? parameters = null,
        CancellationToken cancellationToken = default) =>
        Logic.ConstraintLogic.CallParentOperationAsync(
            Runtime,
            ObjectId,
            ClassName,
            OperationOwnerClass,
            operationName,
            parameters,
            BehaviorResolution,
            cancellationToken);
}
