using Depa.Ontology.Contracts.Models;
using Depa.Ontology.Inputs;
using Depa.Ontology.Internals;
using Depa.Ontology.Runtime;
using Depa.Ontology.Support;
using Depa.Cozo;

namespace Depa.Ontology.Logic;

internal static class ClassLogic
{
    public static async Task DefineClassAsync(CozoOmRuntime runtime, DefineClassInput input, CancellationToken cancellationToken = default)
    {
        var name = OmConvert.RequireName(input.Name, nameof(input.Name));
        var parent = string.IsNullOrWhiteSpace(input.ParentClass)
            ? await ClassExistsAsync(runtime, name, cancellationToken)
                ? await GetParentClassAsync(runtime, name, cancellationToken)
                : null
            : await ResolveClassAsync(runtime, input.ParentClass!, cancellationToken);
        await DefineTypeCoreAsync(runtime, name, input.Description, parent, input.Mixins, cancellationToken);
    }

    public static async Task DefineClassAsync(CozoOmRuntime runtime, DefineClassPatchInput input, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(input.Parent);
        var name = OmConvert.RequireName(input.Name, nameof(input.Name));
        var parent = input.Parent.Kind switch
        {
            ClassParentPatchKind.Keep => await ClassExistsAsync(runtime, name, cancellationToken)
                ? await GetParentClassAsync(runtime, name, cancellationToken)
                : null,
            ClassParentPatchKind.Set => await ResolveClassAsync(runtime, input.Parent.ParentClass!, cancellationToken),
            ClassParentPatchKind.Clear => null,
            _ => throw new ArgumentOutOfRangeException(nameof(input.Parent), input.Parent.Kind, "Unsupported parent patch"),
        };
        await DefineTypeCoreAsync(runtime, name, input.Description, parent, input.Mixins, cancellationToken);
    }

    private static async Task DefineTypeCoreAsync(
        CozoOmRuntime runtime,
        string name,
        string description,
        string? parent,
        IReadOnlyList<string>? mixins,
        CancellationToken cancellationToken)
    {
        if (parent is not null)
        {
            if (!await ClassExistsAsync(runtime, parent, cancellationToken))
            {
                throw new CozoException($"Parent class '{parent}' does not exist");
            }

            var ancestors = await GetAncestorsAsync(runtime, parent, cancellationToken);
            if (parent == name || ancestors.Contains(name))
            {
                throw new CozoException($"Circular inheritance detected: '{name}' -> '{parent}'");
            }
        }

        await runtime.Store.RunAsync(
            CozoScriptBuilder.InputPut("om_class_def", ["class_name"], ["description", "parent_class"]),
            LogicSupport.Params(("class_name", name), ("description", description), ("parent_class", parent)),
            cancellationToken: cancellationToken);

        if (mixins is not null)
        {
            var existing = await GetClassMixinsAsync(runtime, name, cancellationToken);
            foreach (var mixin in existing)
            {
                await runtime.Store.RunAsync(
                    """
                    ?[class_name, mixin_name] <- [[$class_name, $mixin_name]]
                    :rm om_class_mixin {class_name, mixin_name}
                    """,
                    LogicSupport.Params(("class_name", name), ("mixin_name", mixin)),
                    cancellationToken: cancellationToken);
            }

            foreach (var mixin in mixins)
            {
                var mixinName = OmConvert.RequireName(mixin, nameof(mixins));
                if (!await MixinExistsAsync(runtime, mixinName, cancellationToken))
                {
                    throw new CozoException($"Mixin '{mixinName}' does not exist");
                }

                await runtime.Store.RunAsync(
                    CozoScriptBuilder.InputPut("om_class_mixin", ["class_name", "mixin_name"], []),
                    LogicSupport.Params(("class_name", name), ("mixin_name", mixinName)),
                    cancellationToken: cancellationToken);
            }
        }
    }

    public static Task DefineMixinAsync(CozoOmRuntime runtime, DefineMixinInput input, CancellationToken cancellationToken = default)
    {
        return runtime.Store.RunAsync(
            CozoScriptBuilder.InputPut("om_mixin_def", ["name"], ["description"]),
            LogicSupport.Params(("name", OmConvert.RequireName(input.Name, nameof(input.Name))), ("description", input.Description)),
            cancellationToken: cancellationToken);
    }

    public static async Task DefineFieldAsync(CozoOmRuntime runtime, DefineFieldInput input, CancellationToken cancellationToken = default)
    {
        var className = await ResolveClassAsync(runtime, input.ClassName, cancellationToken);
        var fieldName = await ResolveFieldAsync(runtime, className, input.FieldName, cancellationToken);
        var storedType = OmConvert.ValueTypeToStored(input.ValueType);
        if (input.ValueType == OmValueType.Unknown)
        {
            throw new CozoException($"Unsupported field value kind '{input.ValueType}'");
        }

        var inherited = await GetInheritedFieldDefinitionsAsync(runtime, className, cancellationToken);
        if (inherited.TryGetValue(fieldName, out var inheritedDefinition))
        {
            if (inheritedDefinition.ValueType != input.ValueType)
            {
                throw new CozoException($"Cannot change value_kind of '{fieldName}' (inherited as {inheritedDefinition.ValueType})");
            }

            if (inheritedDefinition.Required && !input.Required)
            {
                throw new CozoException($"Cannot loosen required constraint of '{fieldName}' (inherited as required)");
            }
        }

        await runtime.Store.RunAsync(
            CozoScriptBuilder.InputPut("om_field_def", ["class_name", "field_name"], ["value_kind", "required"]),
            LogicSupport.Params(
                ("class_name", className),
                ("field_name", fieldName),
                ("value_kind", storedType),
                ("required", input.Required)),
            cancellationToken: cancellationToken);

        if (!string.IsNullOrWhiteSpace(input.Description))
        {
            await runtime.Store.RunAsync(
                CozoScriptBuilder.InputPut("om_field_desc", ["class_name", "field_name"], ["description"]),
                LogicSupport.Params(("class_name", className), ("field_name", fieldName), ("description", input.Description)),
                cancellationToken: cancellationToken);
        }
    }

    public static async Task DefineRelationDefAsync(CozoOmRuntime runtime, DefineRelationDefInput input, CancellationToken cancellationToken = default)
    {
        var relationName = await ResolveRelationAsync(runtime, input.RelationName, cancellationToken);
        var fromClass = await ResolveClassAsync(runtime, input.FromClass, cancellationToken);
        var toClass = await ResolveClassAsync(runtime, input.ToClass, cancellationToken);

        await runtime.Store.RunAsync(
            CozoScriptBuilder.InputPut("om_relation_def", ["relation_name"], ["from_class", "to_class", "directed"]),
            LogicSupport.Params(
                ("relation_name", relationName),
                ("from_class", fromClass),
                ("to_class", toClass),
                ("directed", input.Directed)),
            cancellationToken: cancellationToken);

        if (!string.IsNullOrWhiteSpace(input.Description))
        {
            await runtime.Store.RunAsync(
                CozoScriptBuilder.InputPut("om_relation_desc", ["relation_name"], ["description"]),
                LogicSupport.Params(("relation_name", relationName), ("description", input.Description)),
                cancellationToken: cancellationToken);
        }
    }

    public static Task DefineClassAliasAsync(CozoOmRuntime runtime, string alias, string canonical, CancellationToken cancellationToken = default)
    {
        return runtime.Store.RunAsync(
            CozoScriptBuilder.InputPut("om_alias_class", ["alias"], ["canonical"]),
            LogicSupport.Params(
                ("alias", OmConvert.RequireName(alias, nameof(alias))),
                ("canonical", OmConvert.RequireName(canonical, nameof(canonical)))),
            cancellationToken: cancellationToken);
    }

    public static Task DefineRelationDefAliasAsync(CozoOmRuntime runtime, string alias, string canonical, CancellationToken cancellationToken = default)
    {
        return runtime.Store.RunAsync(
            CozoScriptBuilder.InputPut("om_alias_relation", ["alias"], ["canonical"]),
            LogicSupport.Params(
                ("alias", OmConvert.RequireName(alias, nameof(alias))),
                ("canonical", OmConvert.RequireName(canonical, nameof(canonical)))),
            cancellationToken: cancellationToken);
    }

    public static Task DefineFieldAliasAsync(CozoOmRuntime runtime, string className, string aliasAttr, string canonicalAttr, CancellationToken cancellationToken = default)
    {
        return runtime.Store.RunAsync(
            CozoScriptBuilder.InputPut("om_alias_field", ["class_name", "alias_field"], ["canonical_field"]),
            LogicSupport.Params(
                ("class_name", OmConvert.RequireName(className, nameof(className))),
                ("alias_field", OmConvert.RequireName(aliasAttr, nameof(aliasAttr))),
                ("canonical_field", OmConvert.RequireName(canonicalAttr, nameof(canonicalAttr)))),
            cancellationToken: cancellationToken);
    }

    public static async Task<string> ResolveClassAsync(CozoOmRuntime runtime, string className, CancellationToken cancellationToken = default)
    {
        var current = OmConvert.RequireName(className, nameof(className));
        var seen = new HashSet<string>(StringComparer.Ordinal);
        while (seen.Add(current))
        {
            var result = await runtime.Store.RunAsync(
                """
                ?[canonical] :=
                  *om_alias_class{ alias: $alias, canonical }
                :limit 1
                """,
                LogicSupport.Params(("alias", current)),
                cancellationToken: cancellationToken);
            if (result.Rows.Count == 0) return current;
            current = JsonRows.StringAt(result.Rows[0], 0) ?? current;
        }

        throw new CozoException($"Class alias cycle detected at '{current}'");
    }

    public static async Task<string> ResolveRelationAsync(CozoOmRuntime runtime, string relationName, CancellationToken cancellationToken = default)
    {
        var current = OmConvert.RequireName(relationName, nameof(relationName));
        var seen = new HashSet<string>(StringComparer.Ordinal);
        while (seen.Add(current))
        {
            var result = await runtime.Store.RunAsync(
                """
                ?[canonical] :=
                  *om_alias_relation{ alias: $alias, canonical }
                :limit 1
                """,
                LogicSupport.Params(("alias", current)),
                cancellationToken: cancellationToken);
            if (result.Rows.Count == 0) return current;
            current = JsonRows.StringAt(result.Rows[0], 0) ?? current;
        }

        throw new CozoException($"Relation alias cycle detected at '{current}'");
    }

    public static async Task<string> ResolveFieldAsync(CozoOmRuntime runtime, string className, string fieldName, CancellationToken cancellationToken = default)
    {
        var canonicalType = await ResolveClassAsync(runtime, className, cancellationToken);
        var current = OmConvert.RequireName(fieldName, nameof(fieldName));
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var aliasScopes = new[] { canonicalType }.Concat(await GetAncestorsAsync(runtime, canonicalType, cancellationToken)).ToArray();
        while (seen.Add(current))
        {
            foreach (var scope in aliasScopes)
            {
                var result = await runtime.Store.RunAsync(
                    """
                    ?[canonical_field] :=
                      *om_alias_field{ class_name: $class_name, alias_field: $alias_field, canonical_field }
                    :limit 1
                    """,
                    LogicSupport.Params(("class_name", scope), ("alias_field", current)),
                    cancellationToken: cancellationToken);
                if (result.Rows.Count == 0) continue;
                current = JsonRows.StringAt(result.Rows[0], 0) ?? current;
                goto NextAlias;
            }

            return current;

        NextAlias:
            continue;
        }

        throw new CozoException($"Field alias cycle detected at '{current}'");
    }

    public static async Task<IReadOnlyList<string>> GetAncestorsAsync(CozoOmRuntime runtime, string className, CancellationToken cancellationToken = default)
    {
        var result = new List<string>();
        var current = await ResolveClassAsync(runtime, className, cancellationToken);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        while (seen.Add(current))
        {
            var parent = await GetParentClassAsync(runtime, current, cancellationToken);
            if (string.IsNullOrWhiteSpace(parent)) break;
            result.Add(parent);
            current = parent;
        }

        return result;
    }

    public static async Task<IReadOnlyList<string>> GetDescendantsAsync(CozoOmRuntime runtime, string className, CancellationToken cancellationToken = default)
    {
        var root = await ResolveClassAsync(runtime, className, cancellationToken);
        var rows = await runtime.Store.RunAsync(
            """
            ?[class_name, parent_class] :=
              *om_class_def{ class_name, description: _d, parent_class }
            """,
            cancellationToken: cancellationToken);
        var children = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        foreach (var row in rows.Rows)
        {
            var name = JsonRows.StringAt(row, 0);
            var parent = JsonRows.StringAt(row, 1);
            if (!string.IsNullOrEmpty(name) && !string.IsNullOrEmpty(parent))
            {
                if (!children.TryGetValue(parent!, out var direct))
                {
                    direct = [];
                    children[parent!] = direct;
                }

                direct.Add(name!);
            }
        }

        var result = new List<string>();
        var queue = new Queue<string>();
        queue.Enqueue(root);
        var visited = new HashSet<string>(StringComparer.Ordinal) { root };
        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            if (!children.TryGetValue(current, out var direct)) continue;
            foreach (var child in direct)
            {
                if (!visited.Add(child)) continue;
                result.Add(child);
                queue.Enqueue(child);
            }
        }

        return result;
    }

    public static async Task<bool> IsSubclassOfAsync(CozoOmRuntime runtime, string childType, string parentClass, CancellationToken cancellationToken = default)
    {
        var child = await ResolveClassAsync(runtime, childType, cancellationToken);
        var parent = await ResolveClassAsync(runtime, parentClass, cancellationToken);
        if (child == parent) return true;
        var ancestors = await GetAncestorsAsync(runtime, child, cancellationToken);
        return ancestors.Contains(parent);
    }

    public static async Task<ClassHierarchyRow> GetClassHierarchyRowAsync(CozoOmRuntime runtime, CancellationToken cancellationToken = default)
    {
        var rows = await runtime.Store.RunAsync(
            """
            ?[class_name, description, parent_class] :=
              *om_class_def{ class_name, description, parent_class }
            :sort class_name
            """,
            cancellationToken: cancellationToken);
        var mixinRows = await runtime.Store.RunAsync(
            """
            ?[class_name, mixin_name] :=
              *om_class_mixin{ class_name, mixin_name }
            :sort class_name, mixin_name
            """,
            cancellationToken: cancellationToken);
        var mixinsByType = mixinRows.Rows
            .GroupBy(row => JsonRows.StringAt(row, 0) ?? "")
            .ToDictionary(g => g.Key, g => (IReadOnlyList<string>)g.Select(row => JsonRows.StringAt(row, 1) ?? "").Where(x => x.Length > 0).ToArray(), StringComparer.Ordinal);
        var mutable = rows.Rows.ToDictionary(
            row => JsonRows.StringAt(row, 0) ?? "",
            row => new MutableTypeNode(
                JsonRows.StringAt(row, 0) ?? "",
                JsonRows.StringAt(row, 1) ?? "",
                string.IsNullOrWhiteSpace(JsonRows.StringAt(row, 2)) ? null : JsonRows.StringAt(row, 2),
                mixinsByType.GetValueOrDefault(JsonRows.StringAt(row, 0) ?? "") ?? [],
                []),
            StringComparer.Ordinal);
        foreach (var node in mutable.Values)
        {
            if (node.ParentClass is not null && mutable.TryGetValue(node.ParentClass, out var parent))
            {
                parent.Children.Add(node.Name);
            }
        }

        var final = mutable.ToDictionary(
            p => p.Key,
            p => new ClassHierarchyNodeRow(p.Value.Name, p.Value.Description, p.Value.ParentClass, p.Value.Mixins.Order().ToArray(), p.Value.Children.Order().ToArray()),
            StringComparer.Ordinal);
        var roots = final.Values.Where(n => n.ParentClass is null || !final.ContainsKey(n.ParentClass)).Select(n => n.Name).Order().ToArray();
        return new ClassHierarchyRow(final, roots);
    }

    internal static async Task<bool> ClassExistsAsync(CozoOmRuntime runtime, string className, CancellationToken cancellationToken)
    {
        var name = await ResolveClassAsync(runtime, className, cancellationToken);
        return await LogicSupport.ExistsAsync(runtime, "om_class_def", "class_name", name, cancellationToken);
    }

    internal static async Task<bool> MixinExistsAsync(CozoOmRuntime runtime, string mixinName, CancellationToken cancellationToken)
    {
        return await LogicSupport.ExistsAsync(runtime, "om_mixin_def", "name", mixinName, cancellationToken);
    }

    internal static async Task<string?> GetParentClassAsync(CozoOmRuntime runtime, string className, CancellationToken cancellationToken)
    {
        var result = await runtime.Store.RunAsync(
            """
            ?[parent_class] :=
              *om_class_def{ class_name: $class_name, description: _d, parent_class }
            :limit 1
            """,
            LogicSupport.Params(("class_name", className)),
            cancellationToken: cancellationToken);
        return result.Rows.Count == 0 ? null : JsonRows.StringAt(result.Rows[0], 0);
    }

    internal static async Task<IReadOnlyList<string>> GetClassMixinsAsync(CozoOmRuntime runtime, string className, CancellationToken cancellationToken)
    {
        var result = await runtime.Store.RunAsync(
            """
            ?[mixin_name] :=
              *om_class_mixin{ class_name: $class_name, mixin_name }
            :sort mixin_name
            """,
            LogicSupport.Params(("class_name", className)),
            cancellationToken: cancellationToken);
        return result.Rows.Select(row => JsonRows.StringAt(row, 0) ?? "").Where(x => x.Length > 0).ToArray();
    }

    public static async Task<IReadOnlyDictionary<string, OmFieldDefinition>> GetFieldDefinitionsAsync(CozoOmRuntime runtime, string className, CancellationToken cancellationToken)
    {
        var canonical = await ResolveClassAsync(runtime, className, cancellationToken);
        var definitions = new Dictionary<string, OmFieldDefinition>(await GetInheritedFieldDefinitionsAsync(runtime, canonical, cancellationToken), StringComparer.Ordinal);
        foreach (var (fieldName, attribute) in await GetOwnFieldDefinitionsAsync(runtime, canonical, cancellationToken))
        {
            definitions[fieldName] = attribute;
        }

        return definitions;
    }

    internal static async Task<IReadOnlyDictionary<string, OmFieldDefinition>> GetInheritedFieldDefinitionsAsync(CozoOmRuntime runtime, string className, CancellationToken cancellationToken)
    {
        var canonical = await ResolveClassAsync(runtime, className, cancellationToken);
        var ancestors = await GetAncestorsAsync(runtime, canonical, cancellationToken);
        var definitions = new Dictionary<string, OmFieldDefinition>(StringComparer.Ordinal);

        var mixins = new List<string>();
        foreach (var mixin in await GetClassMixinsAsync(runtime, canonical, cancellationToken))
        {
            if (!mixins.Contains(mixin, StringComparer.Ordinal)) mixins.Add(mixin);
        }

        foreach (var ancestor in ancestors)
        {
            foreach (var mixin in await GetClassMixinsAsync(runtime, ancestor, cancellationToken))
            {
                if (!mixins.Contains(mixin, StringComparer.Ordinal)) mixins.Add(mixin);
            }
        }

        foreach (var mixin in mixins)
        {
            foreach (var (fieldName, attribute) in await GetOwnFieldDefinitionsAsync(runtime, mixin, cancellationToken))
            {
                definitions[fieldName] = attribute;
            }
        }

        foreach (var currentType in ancestors.Reverse())
        {
            foreach (var (fieldName, attribute) in await GetOwnFieldDefinitionsAsync(runtime, currentType, cancellationToken))
            {
                definitions[fieldName] = attribute;
            }
        }

        return definitions;
    }

    internal static async Task<IReadOnlyDictionary<string, OmFieldDefinition>> GetOwnFieldDefinitionsAsync(CozoOmRuntime runtime, string className, CancellationToken cancellationToken)
    {
        var definitions = new Dictionary<string, OmFieldDefinition>(StringComparer.Ordinal);
        var descRows = await runtime.Store.RunAsync(
            """
            ?[field_name, description] :=
              *om_field_desc{ class_name: $class_name, field_name, description }
            """,
            LogicSupport.Params(("class_name", className)),
            cancellationToken: cancellationToken);
        var descriptions = descRows.Rows.ToDictionary(
            row => JsonRows.StringAt(row, 0) ?? "",
            row => JsonRows.StringAt(row, 1) ?? "",
            StringComparer.Ordinal);

        var rows = await runtime.Store.RunAsync(
            """
            ?[field_name, value_kind, required] :=
              *om_field_def{ class_name: $class_name, field_name, value_kind, required }
            """,
            LogicSupport.Params(("class_name", className)),
            cancellationToken: cancellationToken);
        foreach (var row in rows.Rows)
        {
            var fieldName = JsonRows.StringAt(row, 0) ?? "";
            descriptions.TryGetValue(fieldName, out var description);
            definitions[fieldName] = new OmFieldDefinition(
                className,
                fieldName,
                OmConvert.StoredToValueType(JsonRows.StringAt(row, 1) ?? ""),
                JsonRows.BoolAt(row, 2),
                string.IsNullOrWhiteSpace(description) ? null : description);
        }

        return definitions;
    }

    internal static async Task<IReadOnlyList<string>> GetFieldAliasesForCanonicalAsync(
        CozoOmRuntime runtime,
        string className,
        string canonicalAttr,
        CancellationToken cancellationToken)
    {
        var canonicalType = await ResolveClassAsync(runtime, className, cancellationToken);
        var canonical = await ResolveFieldAsync(runtime, canonicalType, canonicalAttr, cancellationToken);
        var scopes = new[] { canonicalType }.Concat(await GetAncestorsAsync(runtime, canonicalType, cancellationToken)).ToArray();
        var aliases = new List<string>();
        foreach (var scope in scopes)
        {
            var rows = await runtime.Store.RunAsync(
                """
                ?[alias_field] :=
                  *om_alias_field{ class_name: $class_name, alias_field, canonical_field: _canonical_field }
                :sort alias_field
                """,
                LogicSupport.Params(("class_name", scope)),
                cancellationToken: cancellationToken);
            foreach (var row in rows.Rows)
            {
                var alias = JsonRows.StringAt(row, 0);
                if (string.IsNullOrWhiteSpace(alias) || aliases.Contains(alias, StringComparer.Ordinal)) continue;
                try
                {
                    if (await ResolveFieldAsync(runtime, scope, alias!, cancellationToken) == canonical)
                    {
                        aliases.Add(alias!);
                    }
                }
                catch (CozoException)
                {
                    // Cyclic aliases should remain visible to direct resolution errors,
                    // but legacy read fallback should skip them instead of breaking reads.
                }
            }
        }

        aliases.Sort(StringComparer.Ordinal);
        return aliases;
    }

    internal static async Task<OmRelationDef> GetRelationDefinitionAsync(CozoOmRuntime runtime, string relationName, CancellationToken cancellationToken)
    {
        var canonical = await ResolveRelationAsync(runtime, relationName, cancellationToken);
        var rows = await runtime.Store.RunAsync(
            """
            ?[from_class, to_class, directed] :=
              *om_relation_def{ relation_name: $relation_name, from_class, to_class, directed }
            :limit 1
            """,
            LogicSupport.Params(("relation_name", canonical)),
            cancellationToken: cancellationToken);
        if (rows.Rows.Count == 0)
        {
            throw new CozoException($"Relation '{canonical}' is not defined");
        }

        var row = rows.Rows[0];
        return new OmRelationDef(canonical, JsonRows.StringAt(row, 0) ?? "", JsonRows.StringAt(row, 1) ?? "", JsonRows.BoolAt(row, 2));
    }

    private sealed record MutableTypeNode(string Name, string Description, string? ParentClass, IReadOnlyList<string> Mixins, List<string> Children);
}
