using Depa.Ontology.Contracts.Models;
using Depa.Ontology.Runtime;

namespace Depa.Ontology.Logic;

internal static class BehaviorReadinessLogic
{
    internal static async Task EnsureReadyIfBoundAsync(
        CozoOmRuntime runtime,
        BehaviorResolutionScope resolution,
        BehaviorBindingKey key,
        CancellationToken cancellationToken)
    {
        var binding = await BehaviorBindingLogic.QueryAsync(runtime, key, cancellationToken);
        if (binding is null)
        {
            return;
        }

        if (!resolution.RegistrySnapshot.TryGetBindingId(key, out var registeredBindingId)
            || !string.Equals(binding.BindingId, registeredBindingId, StringComparison.Ordinal))
        {
            throw new BehaviorUnresolvedException(CreateDiagnostic(binding));
        }
    }

    private static BehaviorUnresolvedDiagnostic CreateDiagnostic(BehaviorBindingRow binding)
    {
        var key = binding.Key;
        var kind = ToPublicKind(key.BehaviorKind);
        var phase = key.BehaviorKind == BehaviorKind.Interceptor ? key.Phase : null;
        int? seq = key.BehaviorKind == BehaviorKind.Interceptor ? key.Seq : null;
        return new BehaviorUnresolvedDiagnostic(
            "OMR1001",
            kind,
            key.OwnerClass,
            DisplayBehaviorKey(kind, key.OwnerClass, key.BehaviorName, phase, seq),
            ToPublicSlot(key.CallbackSlot),
            binding.BindingId,
            phase,
            seq);
    }

    private static string DisplayBehaviorKey(
        BehaviorCatalogKind kind,
        string owner,
        string name,
        string? phase,
        int? seq) =>
        kind == BehaviorCatalogKind.Interceptor
            ? $"{KindWire(kind)}:{owner}/{name}/{phase}/{seq}"
            : $"{KindWire(kind)}:{owner}/{name}";

    private static BehaviorCatalogKind ToPublicKind(BehaviorKind kind) => kind switch
    {
        BehaviorKind.Constraint => BehaviorCatalogKind.Constraint,
        BehaviorKind.ComputedProp => BehaviorCatalogKind.ComputedProp,
        BehaviorKind.Operation => BehaviorCatalogKind.Operation,
        BehaviorKind.Mutation => BehaviorCatalogKind.Mutation,
        BehaviorKind.Interceptor => BehaviorCatalogKind.Interceptor,
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown behavior kind"),
    };

    private static BehaviorCatalogCallbackSlot ToPublicSlot(BehaviorCallbackSlot slot) => slot switch
    {
        BehaviorCallbackSlot.When => BehaviorCatalogCallbackSlot.When,
        BehaviorCallbackSlot.Then => BehaviorCatalogCallbackSlot.Then,
        BehaviorCallbackSlot.Validator => BehaviorCatalogCallbackSlot.Validator,
        BehaviorCallbackSlot.Compute => BehaviorCatalogCallbackSlot.Compute,
        BehaviorCallbackSlot.Handler => BehaviorCatalogCallbackSlot.Handler,
        BehaviorCallbackSlot.Executor => BehaviorCatalogCallbackSlot.Executor,
        _ => throw new ArgumentOutOfRangeException(nameof(slot), slot, "Unknown callback slot"),
    };

    private static string KindWire(BehaviorCatalogKind kind) => kind switch
    {
        BehaviorCatalogKind.Constraint => "constraint",
        BehaviorCatalogKind.ComputedProp => "computedProp",
        BehaviorCatalogKind.Operation => "operation",
        BehaviorCatalogKind.Mutation => "mutation",
        BehaviorCatalogKind.Interceptor => "interceptor",
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown behavior kind"),
    };
}
