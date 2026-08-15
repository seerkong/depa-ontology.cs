using System.Collections.Immutable;
using Depa.Ontology.Runtime;

namespace Depa.Ontology.Contracts.Models;

public sealed record BehaviorImportOptions(bool RequireReady = false);

public sealed record BehaviorConstraintCallbackBinding(
    string BindingId,
    Func<OmValidationContext, ValueTask<bool>> Callback);

public sealed record BehaviorValidatorCallbackBinding(
    string BindingId,
    Func<OmValidationContext, ValueTask<string?>> Callback);

public sealed record BehaviorComputedPropCallbackBinding(
    string BindingId,
    Func<OmComputedPropContext, ValueTask<object?>> Callback);

public sealed record BehaviorOperationCallbackBinding(
    string BindingId,
    Func<OmOperationContext, IReadOnlyDictionary<string, object?>, ValueTask<IReadOnlyList<MutationSpec>>> Callback);

public sealed record BehaviorMutationCallbackBinding(
    string BindingId,
    Func<OmMutationContext, IReadOnlyDictionary<string, object?>, ValueTask> Callback);

public sealed record BehaviorInterceptorCallbackBinding(
    string BindingId,
    Func<OmOperationContext, ValueTask> Callback);

public sealed record BehaviorCallbackBindingSet
{
    public BehaviorCallbackBindingSet(
        IEnumerable<BehaviorConstraintCallbackBinding>? constraints = null,
        IEnumerable<BehaviorValidatorCallbackBinding>? validators = null,
        IEnumerable<BehaviorComputedPropCallbackBinding>? computedProp = null,
        IEnumerable<BehaviorOperationCallbackBinding>? operations = null,
        IEnumerable<BehaviorMutationCallbackBinding>? mutations = null,
        IEnumerable<BehaviorInterceptorCallbackBinding>? interceptors = null)
    {
        Constraints = Copy(constraints);
        Validators = Copy(validators);
        ComputedProps = Copy(computedProp);
        Operations = Copy(operations);
        Mutations = Copy(mutations);
        Interceptors = Copy(interceptors);
    }

    public ImmutableArray<BehaviorConstraintCallbackBinding> Constraints { get; }
    public ImmutableArray<BehaviorValidatorCallbackBinding> Validators { get; }
    public ImmutableArray<BehaviorComputedPropCallbackBinding> ComputedProps { get; }
    public ImmutableArray<BehaviorOperationCallbackBinding> Operations { get; }
    public ImmutableArray<BehaviorMutationCallbackBinding> Mutations { get; }
    public ImmutableArray<BehaviorInterceptorCallbackBinding> Interceptors { get; }

    private static ImmutableArray<T> Copy<T>(IEnumerable<T>? values) =>
        values?.ToImmutableArray() ?? ImmutableArray<T>.Empty;
}

public sealed record BehaviorImportDiagnostic(
    string Code,
    string Path,
    string Message,
    BehaviorCatalogKind? Kind = null,
    string? OwnerClass = null,
    string? BehaviorName = null,
    BehaviorCatalogCallbackSlot? Slot = null,
    string? BindingId = null,
    string? InterceptorPhase = null,
    int? InterceptorSeq = null);

public sealed record BehaviorImportResult
{
    public BehaviorImportResult(
        bool applied,
        IEnumerable<BehaviorImportDiagnostic>? diagnostics = null,
        IEnumerable<BehaviorUnresolvedDiagnostic>? unresolved = null)
    {
        Applied = applied;
        Diagnostics = diagnostics?.ToImmutableArray() ?? ImmutableArray<BehaviorImportDiagnostic>.Empty;
        Unresolved = unresolved?.ToImmutableArray() ?? ImmutableArray<BehaviorUnresolvedDiagnostic>.Empty;
    }

    public bool Applied { get; }
    public ImmutableArray<BehaviorImportDiagnostic> Diagnostics { get; }
    public ImmutableArray<BehaviorUnresolvedDiagnostic> Unresolved { get; }
}

public sealed class BehaviorImportException : Exception
{
    public BehaviorImportException(
        string message,
        Exception originalFailure,
        IEnumerable<Exception>? compensationFailures = null)
        : base(message, originalFailure)
    {
        OriginalFailure = originalFailure;
        CompensationFailures = compensationFailures?.ToImmutableArray() ?? ImmutableArray<Exception>.Empty;
    }

    public Exception OriginalFailure { get; }
    public ImmutableArray<Exception> CompensationFailures { get; }
}
