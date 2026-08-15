using System.Reflection;
using Depa.Cozo;
using Depa.Ontology;
using Depa.Ontology.Contracts;
using Depa.Ontology.Contracts.Models;
using Depa.Ontology.Inputs;
using Depa.Ontology.Runtime;

internal static class PublicSurfaceParityFixtures
{
    public static async Task RunAsync()
    {
        using var db = new CozoDb(engine: "mem", path: "");
        var om = new CozoOm(db);
        await om.InitSchemaAsync();

        await om.DefineClassAsync("PublicSurfaceParent", "parent");
        await om.DefineClassAsync("PublicSurfaceChild", "child", parentClass: "PublicSurfaceParent");
        await om.DefineClassAsync("PublicSurfaceChild", "child rewritten", parentClass: null);
        Require(
            await om.IsSubclassOfAsync("PublicSurfaceChild", "PublicSurfaceParent"),
            "nullable DefineClassAsync parent input must preserve an existing parent class");

        var publicMethods = typeof(CozoOm).GetMethods(BindingFlags.Instance | BindingFlags.Public);
        Require(
            publicMethods.Any(method => method.Name == "ValidateConstraintsAsync"),
            "CozoOm must expose a public ValidateConstraintsAsync facade");
        Require(
            publicMethods.Any(method => method.Name == "TraverseAsync"),
            "CozoOm must expose a public TraverseAsync facade");
        Require(
            publicMethods.Any(method => method.Name == "InferValueType"),
            "CozoOm must expose a public InferValueType facade");
        Require(
            publicMethods.Any(method => method.Name == "DefineClassAsync"
                                     && method.GetParameters().Any(parameter => parameter.ParameterType.Name.Contains("Patch", StringComparison.Ordinal))),
            "CozoOm must expose an explicit Keep/Set/Clear parent patch API");
        Require(
            publicMethods.Any(method => method.Name == "DefineRelationDefAsync")
            && publicMethods.Any(method => method.Name == "CreateRelationLinkAsync")
            && publicMethods.Any(method => method.Name == "RetractRelationLinkAsync")
            && publicMethods.Any(method => method.Name == "DefineComputedPropAsync")
            && publicMethods.Any(method => method.Name == "DefineOperationAsync")
            && publicMethods.Any(method => method.Name == "ExecuteOperationAsync"),
            "CozoOm must expose RelationDef, RelationLink, ComputedProp, and Operation public APIs");
        Require(
            publicMethods.All(method => method.Name is not "DefineRelationAsync"
                                           and not "LinkEntitiesAsync"
                                           and not "UnlinkEntitiesAsync"
                                           and not "DefineComputedAsync"
                                           and not "DefineActionAsync"
                                           and not "ExecuteActionAsync"),
            "CozoOm must not expose old Relation/Entity/Computed/Action compatibility aliases");

        Require(
            ClassParentPatch.Keep is { Kind: ClassParentPatchKind.Keep, ParentClass: null }
            && ClassParentPatch.Clear is { Kind: ClassParentPatchKind.Clear, ParentClass: null }
            && ClassParentPatch.Set("PublicSurfaceParent") is { Kind: ClassParentPatchKind.Set, ParentClass: "PublicSurfaceParent" },
            "class parent patch contracts must preserve distinct Keep, Set, and Clear intents");
        await ExpectArgumentFailureAsync(
            () => Task.Run(() => ClassParentPatch.Set(" ")),
            "a Set parent patch must require a concrete parent class");

        await om.DefineClassAsync(new DefineClassPatchInput(
            "PublicSurfaceChild",
            "child cleared",
            ClassParentPatch.Clear));
        Require(
            !await om.IsSubclassOfAsync("PublicSurfaceChild", "PublicSurfaceParent"),
            "an explicit Clear parent patch must remove the existing parent");
        await om.DefineClassAsync(new DefineClassPatchInput(
            "PublicSurfaceChild",
            "child reset",
            ClassParentPatch.Set("PublicSurfaceParent")));
        await om.DefineClassAsync(new DefineClassPatchInput(
            "PublicSurfaceChild",
            "child kept",
            ClassParentPatch.Keep));
        Require(
            await om.IsSubclassOfAsync("PublicSurfaceChild", "PublicSurfaceParent"),
            "an explicit Keep parent patch must retain the existing parent");

        await om.DefineFieldAsync("PublicSurfaceParent", "required_name", OmValueType.String, required: true);
        await om.CreateObjectAsync("public-surface:entity", "PublicSurfaceParent", "entity");
        await om.DefineConstraintAsync("PublicSurfaceParent", "public_surface_constraint", "custom");
        om.RegisterValidator(
            "PublicSurfaceParent",
            "public_surface_constraint",
            _ => ValueTask.FromResult<string?>("public constraint failure"));
        Require(
            !(await om.ValidateConstraintsAsync("public-surface:entity", ["custom"])).Valid,
            "the public constraint facade must preserve validation errors");
        Require(
            om.InferValueType("text") == OmValueType.String
            && om.InferValueType(42) == OmValueType.Number
            && om.InferValueType(true) == OmValueType.Bool
            && om.InferValueType(null) == OmValueType.Unknown,
            "the public value type facade must preserve internal inference semantics");

        await om.DefineClassAsync("PublicSurfaceTarget", "target");
        await om.DefineRelationDefAsync("public_surface_rel", "PublicSurfaceParent", "PublicSurfaceTarget");
        await om.CreateObjectAsync("public-surface:target", "PublicSurfaceTarget", "target");
        await om.CreateRelationLinkAsync("public-surface:entity", "public_surface_rel", "public-surface:target", options: new WriteOptions(SkipConstraints: true));
        Require(
            (await om.TraverseAsync("public-surface:entity", [])).Select(entity => entity.Id).SequenceEqual(["public-surface:entity"])
            && (await om.TraverseAsync("public-surface:entity", ["public_surface_rel"])).Select(entity => entity.Id).SequenceEqual(["public-surface:target"]),
            "the public traversal facade must return the start node for an empty path and outgoing endpoints for a relation path");

        var alwaysTrue = new Func<OmValidationContext, ValueTask<bool>>(_ => ValueTask.FromResult(true));
        const string missingOwner = "PublicSurfaceMissingOwner";
        await ExpectCozoFailureAsync(
            () => om.AddInterceptorAsync(missingOwner, "missing_owner_operation", "before", _ => ValueTask.CompletedTask),
            "missing behavior owner must fail before registration");
        await ExpectArgumentFailureAsync(
            () => om.DefineConstraintAsync("PublicSurfaceParent", "invalid_scope", "request", alwaysTrue, alwaysTrue),
            "invalid constraint scope must fail before registration");
        await ExpectArgumentFailureAsync(
            () => om.AddInterceptorAsync("PublicSurfaceParent", "invalid_phase_operation", "around", _ => ValueTask.CompletedTask),
            "invalid interceptor phase must fail before registration");

        Require(
            DefinitionCount(db, "interceptor", missingOwner, "missing_owner_operation") == 0
            && DefinitionCount(db, "constraint", "PublicSurfaceParent", "invalid_scope") == 0
            && DefinitionCount(db, "interceptor", "PublicSurfaceParent", "invalid_phase_operation") == 0,
            "invalid behavior definitions must not leave persistent metadata");
        Require(
            om.Runtime.Registry.GetInterceptors(missingOwner, "missing_owner_operation", "before").Count == 0
            && !om.Runtime.Registry.TryGetConstraint("PublicSurfaceParent", "invalid_scope", out _)
            && om.Runtime.Registry.GetInterceptors("PublicSurfaceParent", "invalid_phase_operation", "before").Count == 0
            && om.Runtime.Registry.GetInterceptors("PublicSurfaceParent", "invalid_phase_operation", "after").Count == 0,
            "invalid behavior definitions must not leave callback registry ghosts");
    }

    private static int DefinitionCount(CozoDb db, string kind, string owner, string name)
    {
        var script = kind switch
        {
            "constraint" => "?[class_name, constraint_name] := *om_constraint_def{class_name, constraint_name}, class_name = $owner, constraint_name = $name",
            "interceptor" => "?[class_name, operation_name] := *om_interceptor_def{class_name, operation_name}, class_name = $owner, operation_name = $name",
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unsupported definition kind"),
        };
        using var result = db.Run(script, new { owner, name });
        return result.RootElement.GetProperty("rows").GetArrayLength();
    }

    private static async Task ExpectCozoFailureAsync(Func<Task> operation, string message)
    {
        try { await operation(); }
        catch (CozoException) { return; }
        throw new InvalidOperationException(message);
    }

    private static async Task ExpectArgumentFailureAsync(Func<Task> operation, string message)
    {
        try { await operation(); }
        catch (ArgumentException) { return; }
        throw new InvalidOperationException(message);
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
