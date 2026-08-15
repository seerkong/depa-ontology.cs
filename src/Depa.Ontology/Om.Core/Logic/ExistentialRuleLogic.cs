using System.Text.Json;
using Depa.Cozo;
using Depa.Ontology.Contracts.Models;
using Depa.Ontology.Inputs;
using Depa.Ontology.Internals;
using Depa.Ontology.Runtime;
using Depa.Ontology.Support;

namespace Depa.Ontology.Logic;

public static class ExistentialRuleLogic
{
    private const string SkolemOriginAttr = "_skolem_rule";

    public static async Task<ExistentialRule> DefineExistentialRuleAsync(
        CozoOmRuntime runtime,
        DefineExistentialRuleInput input,
        CancellationToken cancellationToken = default)
    {
        var ruleName = OmConvert.RequireName(input.RuleName, nameof(input.RuleName));
        var normalized = await NormalizeSpecAsync(runtime, input.Spec, cancellationToken);
        var mode = normalized.Mode == ExistentialRuleMode.Materialize ? "materialize" : "check";
        var specJson = JsonSerializer.Serialize(new
        {
            forEach = new
            {
                type = normalized.ForEach.Type,
                where = normalized.ForEach.Where
            },
            exists = new
            {
                rel = normalized.Exists.Rel,
                direction = normalized.Exists.Direction == ExistentialDirection.In ? "in" : "out",
                toClass = normalized.Exists.ToClass
            },
            materialize = normalized.Materialize
        }, OmConvert.JsonOptions);

        if (normalized.Mode == ExistentialRuleMode.Materialize)
        {
            await ClassLogic.DefineFieldAsync(
                runtime,
                new DefineFieldInput(normalized.Exists.ToClass, SkolemOriginAttr, OmValueType.String, false, "Skolem origin rule"),
                cancellationToken);
        }

        await runtime.Store.RunAsync(
            CozoScriptBuilder.InputPut("om_existential_rule_def", ["rule_name"], ["spec_json", "mode", "message", "enabled"]),
            LogicSupport.Params(
                ("rule_name", ruleName),
                ("spec_json", specJson),
                ("mode", mode),
                ("message", normalized.Message),
                ("enabled", normalized.Enabled)),
            cancellationToken: cancellationToken);

        return ToRule(ruleName, normalized);
    }

    public static async Task<IReadOnlyList<ExistentialRule>> ListExistentialRulesAsync(CozoOmRuntime runtime, CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await runtime.Store.RunAsync(
                """
                ?[rule_name, spec_json, mode, message, enabled] :=
                  *om_existential_rule_def{ rule_name, spec_json, mode, message, enabled }
                :sort rule_name
                """,
                cancellationToken: cancellationToken);
            return result.Rows.Select(ParseRuleRow).Where(r => r is not null).Cast<ExistentialRule>().ToArray();
        }
        catch (CozoException exception) when (IsMissingRuleStorage(exception))
        {
            return [];
        }
    }

    public static async Task<IReadOnlyList<ExistentialViolation>> CheckExistentialRulesAsync(
        CozoOmRuntime runtime,
        CheckExistentialRulesInput? input = null,
        CancellationToken cancellationToken = default)
    {
        var wanted = input?.Rules is { Count: > 0 } ? new HashSet<string>(input.Rules, StringComparer.Ordinal) : null;
        var asOf = string.IsNullOrWhiteSpace(input?.AsOf) ? null : OmConvert.NormalizeTimestamp(input!.AsOf!, nameof(input.AsOf));
        var output = new List<ExistentialViolation>();
        foreach (var rule in await ListExistentialRulesAsync(runtime, cancellationToken))
        {
            if (!rule.Enabled) continue;
            if (wanted is not null && !wanted.Contains(rule.RuleName)) continue;
            output.AddRange(await FindViolationsAsync(runtime, rule, asOf, cancellationToken));
        }

        return output.OrderBy(v => v.Rule, StringComparer.Ordinal).ThenBy(v => v.ObjectId, StringComparer.Ordinal).ToArray();
    }

    public static async Task<ExistentialChaseResult> ApplyExistentialRulesAsync(
        CozoOmRuntime runtime,
        ApplyExistentialRulesInput? input = null,
        CancellationToken cancellationToken = default)
    {
        var maxIterations = input?.MaxIterations ?? runtime.Options.DefaultMaxChaseIterations;
        if (maxIterations < 1) throw new ArgumentOutOfRangeException(nameof(input.MaxIterations), "maxIterations must be positive");
        var wanted = input?.Rules is { Count: > 0 } ? new HashSet<string>(input.Rules, StringComparer.Ordinal) : null;
        var rules = (await ListExistentialRulesAsync(runtime, cancellationToken))
            .Where(r => r.Enabled && r.Mode == ExistentialRuleMode.Materialize)
            .Where(r => wanted is null || wanted.Contains(r.RuleName))
            .ToArray();
        var created = new List<ExistentialCreated>();
        var attempted = new HashSet<string>(StringComparer.Ordinal);
        var iterations = 0;

        for (var iter = 1; iter <= maxIterations; iter++)
        {
            iterations = iter;
            var roundCreated = 0;
            foreach (var rule in rules)
            {
                var runtimeRule = await ResolveRuntimeRuleAsync(runtime, rule, cancellationToken);
                var violations = await FindViolationsAsync(runtime, runtimeRule, asOf: null, cancellationToken);
                foreach (var violation in violations)
                {
                    var attemptKey = $"{runtimeRule.RuleName}\u0001{violation.ObjectId}";
                    if (!attempted.Add(attemptKey)) continue;
                    var skolemId = OmConvert.SkolemId(runtimeRule.RuleName, violation.ObjectId);
                    var label = SkolemLabel(runtimeRule, violation.ObjectId);
                    await ObjectLogic.UpsertObjectAsync(runtime, new ObjectInput(skolemId, runtimeRule.Exists.ToClass, label), cancellationToken);
                    await ObjectLogic.SetFieldValueAsync(
                        runtime,
                        new SetFieldValueInput(skolemId, SkolemOriginAttr, runtimeRule.RuleName, new WriteOptions(SkipConstraints: true, ValidTime: input?.ValidTime)),
                        cancellationToken);

                    foreach (var prop in runtimeRule.Materialize?.Payload ?? new Dictionary<string, JsonElement>())
                    {
                        await ObjectLogic.SetFieldValueAsync(
                            runtime,
                            new SetFieldValueInput(skolemId, prop.Key, prop.Value, new WriteOptions(SkipConstraints: true, ValidTime: input?.ValidTime)),
                            cancellationToken);
                    }

                    var link = runtimeRule.Exists.Direction == ExistentialDirection.In
                        ? new CreateRelationLinkInput(skolemId, runtimeRule.Exists.Rel, violation.ObjectId, new Dictionary<string, object?>(), new WriteOptions(SkipConstraints: true, ValidTime: input?.ValidTime))
                        : new CreateRelationLinkInput(violation.ObjectId, runtimeRule.Exists.Rel, skolemId, new Dictionary<string, object?>(), new WriteOptions(SkipConstraints: true, ValidTime: input?.ValidTime));
                    await RelationLogic.CreateRelationLinkAsync(runtime, link, cancellationToken);
                    created.Add(new ExistentialCreated(runtimeRule.RuleName, violation.ObjectId, skolemId, runtimeRule.Exists.Rel, runtimeRule.Exists.ToClass));
                    roundCreated++;
                }
            }

            if (roundCreated == 0) break;
        }

        var diagnostics = new List<ExistentialDiagnostic>();
        foreach (var rule in rules)
        {
            var remaining = await FindViolationsAsync(runtime, rule, asOf: null, cancellationToken);
            if (remaining.Count > 0) diagnostics.Add(new ExistentialDiagnostic(rule.RuleName, remaining.Count));
        }

        return new ExistentialChaseResult(created, iterations, diagnostics.Count == 0, diagnostics);
    }

    private static async Task<ExistentialRuleSpec> NormalizeSpecAsync(CozoOmRuntime runtime, ExistentialRuleSpec spec, CancellationToken cancellationToken)
    {
        var bodyType = await ClassLogic.ResolveClassAsync(runtime, spec.ForEach.Type, cancellationToken);
        if (!await ClassLogic.ClassExistsAsync(runtime, bodyType, cancellationToken))
        {
            throw new CozoException($"Unknown type '{bodyType}' in forEach.type");
        }

        var rel = await ClassLogic.ResolveRelationAsync(runtime, spec.Exists.Rel, cancellationToken);
        _ = await ClassLogic.GetRelationDefinitionAsync(runtime, rel, cancellationToken);
        var toClass = await ClassLogic.ResolveClassAsync(runtime, spec.Exists.ToClass, cancellationToken);
        if (!await ClassLogic.ClassExistsAsync(runtime, toClass, cancellationToken))
        {
            throw new CozoException($"Unknown type '{toClass}' in exists.toClass");
        }

        var where = new List<ExistentialWhereCondition>();
        foreach (var condition in spec.ForEach.Where ?? [])
        {
            if (condition.Op is not ("=" or "!=" or ">" or ">=" or "<" or "<="))
            {
                throw new CozoException($"Unsupported existential where op '{condition.Op}'");
            }

            var attr = await ClassLogic.ResolveFieldAsync(runtime, bodyType, condition.Attr, cancellationToken);
            where.Add(condition with { Attr = attr });
        }

        return spec with
        {
            ForEach = new ExistentialForEachSpec(bodyType, where),
            Exists = new ExistentialExistsSpec(rel, spec.Exists.Direction, toClass),
            Message = spec.Message ?? "",
            Enabled = spec.Enabled
        };
    }

    private static async Task<IReadOnlyList<ExistentialViolation>> FindViolationsAsync(
        CozoOmRuntime runtime,
        ExistentialRule rule,
        string? asOf,
        CancellationToken cancellationToken)
    {
        rule = await ResolveRuntimeRuleAsync(runtime, rule, cancellationToken);
        var bodyTypes = new[] { rule.ForEach.Type }
            .Concat(await ClassLogic.GetDescendantsAsync(runtime, rule.ForEach.Type, cancellationToken))
            .ToArray();
        var bodyClassNames = await ExpandClassNamesAsync(runtime, bodyTypes, cancellationToken);
        var toClasss = new[] { rule.Exists.ToClass }
            .Concat(await ClassLogic.GetDescendantsAsync(runtime, rule.Exists.ToClass, cancellationToken))
            .ToArray();
        var toClassNames = await ExpandClassNamesAsync(runtime, toClasss, cancellationToken);
        var relationNames = await ExpandRelationNamesAsync(runtime, rule.Exists.Rel, cancellationToken);
        var at = asOf is null ? "\"NOW\"" : "$as_of";
        var relationLinkAtom = rule.Exists.Direction == ExistentialDirection.In
            ? $"*om_relation_link{{ from_object_id: other_id, relation_name: rn, to_object_id: id, payload: _p @ {at} }}"
            : $"*om_relation_link{{ from_object_id: id, relation_name: rn, to_object_id: other_id, payload: _p @ {at} }}";
        var parameters = LogicSupport.Params(("relation_names", relationNames), ("to_classes", toClassNames));
        if (asOf is not null) parameters["as_of"] = asOf;

        var whereAtoms = new List<string>();
        var index = 0;
        foreach (var condition in rule.ForEach.Where ?? [])
        {
            var op = condition.Op switch
            {
                "=" => "==",
                "!=" => "!=",
                ">" => ">",
                ">=" => ">=",
                "<" => "<",
                "<=" => "<=",
                _ => throw new CozoException($"Unsupported existential where op '{condition.Op}'")
            };
            parameters[$"w_attrs_{index}"] = await ExpandAttributeNamesAsync(runtime, bodyTypes, condition.Attr, cancellationToken);
            parameters[$"w_value_{index}"] = JsonSerializer.Deserialize<object?>(condition.Value.GetRawText(), OmConvert.JsonOptions);
            whereAtoms.Add($"*om_field_value{{ object_id: id, field_name: w_attr_{index}, value: w_val_{index} @ {at} }}");
            whereAtoms.Add($"is_in(w_attr_{index}, $w_attrs_{index})");
            whereAtoms.Add($"w_val_{index} {op} $w_value_{index}");
            index++;
        }

        var where = whereAtoms.Count == 0 ? "" : ",\n  " + string.Join(",\n  ", whereAtoms);
        var script =
            $"sat[id] := {relationLinkAtom}, is_in(rn, $relation_names),\n" +
            "  *om_object{ id: other_id, class_name: other_type, label: _other_label },\n" +
            "  is_in(other_type, $to_classes)\n" +
            "?[id] := *om_object{ id, class_name: $class_name, label: _label }" + where + ",\n" +
            "  not sat[id]\n" +
            ":sort id";

        var violations = new List<ExistentialViolation>();
        foreach (var bodyType in bodyClassNames)
        {
            parameters["class_name"] = bodyType;
            var rows = await runtime.Store.RunAsync(script, parameters, cancellationToken: cancellationToken);
            violations.AddRange(rows.Rows.Select(row => new ExistentialViolation(rule.RuleName, JsonRows.StringAt(row, 0) ?? "", rule.Message)));
        }

        return violations;
    }

    private static async Task<IReadOnlyList<string>> ExpandClassNamesAsync(
        CozoOmRuntime runtime,
        IEnumerable<string> canonicalTypes,
        CancellationToken cancellationToken)
    {
        var names = new SortedSet<string>(StringComparer.Ordinal);
        foreach (var className in canonicalTypes)
        {
            var canonical = await ClassLogic.ResolveClassAsync(runtime, className, cancellationToken);
            names.Add(canonical);
            foreach (var alias in await ListTypeAliasesForCanonicalAsync(runtime, canonical, cancellationToken)) names.Add(alias);
        }

        return names.ToArray();
    }

    private static async Task<IReadOnlyList<string>> ExpandRelationNamesAsync(
        CozoOmRuntime runtime,
        string canonicalRelation,
        CancellationToken cancellationToken)
    {
        var canonical = await ClassLogic.ResolveRelationAsync(runtime, canonicalRelation, cancellationToken);
        var names = new SortedSet<string>(StringComparer.Ordinal) { canonical };
        foreach (var alias in await ListRelationAliasesForCanonicalAsync(runtime, canonical, cancellationToken)) names.Add(alias);
        return names.ToArray();
    }

    private static async Task<IReadOnlyList<string>> ExpandAttributeNamesAsync(
        CozoOmRuntime runtime,
        IEnumerable<string> bodyTypes,
        string canonicalAttribute,
        CancellationToken cancellationToken)
    {
        var names = new SortedSet<string>(StringComparer.Ordinal);
        foreach (var className in bodyTypes)
        {
            var canonical = await ClassLogic.ResolveFieldAsync(runtime, className, canonicalAttribute, cancellationToken);
            names.Add(canonical);
            foreach (var alias in await ClassLogic.GetFieldAliasesForCanonicalAsync(runtime, className, canonical, cancellationToken)) names.Add(alias);
        }

        return names.ToArray();
    }

    private static async Task<IReadOnlyList<string>> ListTypeAliasesForCanonicalAsync(
        CozoOmRuntime runtime,
        string canonicalType,
        CancellationToken cancellationToken)
    {
        var rows = await runtime.Store.RunAsync(
            """
            ?[alias] :=
              *om_alias_class{ alias, canonical: _canonical }
            :sort alias
            """,
            cancellationToken: cancellationToken);
        return await FilterAliasesAsync(rows.Rows, alias => ClassLogic.ResolveClassAsync(runtime, alias, cancellationToken), canonicalType);
    }

    private static async Task<IReadOnlyList<string>> ListRelationAliasesForCanonicalAsync(
        CozoOmRuntime runtime,
        string canonicalRelation,
        CancellationToken cancellationToken)
    {
        var rows = await runtime.Store.RunAsync(
            """
            ?[alias] :=
              *om_alias_relation{ alias, canonical: _canonical }
            :sort alias
            """,
            cancellationToken: cancellationToken);
        return await FilterAliasesAsync(rows.Rows, alias => ClassLogic.ResolveRelationAsync(runtime, alias, cancellationToken), canonicalRelation);
    }

    private static async Task<IReadOnlyList<string>> FilterAliasesAsync(
        IReadOnlyList<IReadOnlyList<JsonElement>> rows,
        Func<string, Task<string>> resolve,
        string canonical)
    {
        var aliases = new List<string>();
        foreach (var row in rows)
        {
            var alias = JsonRows.StringAt(row, 0);
            if (string.IsNullOrWhiteSpace(alias)) continue;
            try
            {
                if (await resolve(alias!) == canonical) aliases.Add(alias!);
            }
            catch (CozoException)
            {
                // Cyclic legacy aliases remain resolution errors, but must not break compatible reads.
            }
        }

        return aliases;
    }

    private static async Task<ExistentialRule> ResolveRuntimeRuleAsync(
        CozoOmRuntime runtime,
        ExistentialRule rule,
        CancellationToken cancellationToken)
    {
        var bodyType = await ClassLogic.ResolveClassAsync(runtime, rule.ForEach.Type, cancellationToken);
        var relation = await ClassLogic.ResolveRelationAsync(runtime, rule.Exists.Rel, cancellationToken);
        var toClass = await ClassLogic.ResolveClassAsync(runtime, rule.Exists.ToClass, cancellationToken);
        var where = new List<ExistentialWhereCondition>();
        foreach (var condition in rule.ForEach.Where ?? [])
        {
            where.Add(condition with
            {
                Attr = await ClassLogic.ResolveFieldAsync(runtime, bodyType, condition.Attr, cancellationToken),
            });
        }

        return rule with
        {
            ForEach = new ExistentialForEachSpec(bodyType, where),
            Exists = new ExistentialExistsSpec(relation, rule.Exists.Direction, toClass),
        };
    }

    private static bool IsMissingRuleStorage(CozoException exception)
    {
        var detail = $"{exception.Message}\n{exception.RawResponse}";
        return detail.Contains("relation not found", StringComparison.OrdinalIgnoreCase)
               || detail.Contains("stored_relation_not_found", StringComparison.OrdinalIgnoreCase)
               || detail.Contains("cannot find requested stored relation", StringComparison.OrdinalIgnoreCase)
               || detail.Contains("not found", StringComparison.OrdinalIgnoreCase) && detail.Contains("om_existential_rule_def", StringComparison.Ordinal);
    }

    private static string SkolemLabel(ExistentialRule rule, string triggerObjectId)
    {
        var template = rule.Materialize?.LabelTemplate;
        return string.IsNullOrWhiteSpace(template)
            ? $"skolem:{rule.RuleName}:{triggerObjectId}"
            : template!.Replace("{fromId}", triggerObjectId, StringComparison.Ordinal).Replace("{rule}", rule.RuleName, StringComparison.Ordinal);
    }

    private static ExistentialRule ToRule(string ruleName, ExistentialRuleSpec spec)
    {
        return new ExistentialRule(ruleName, spec.ForEach, spec.Exists, spec.Materialize, spec.Mode, spec.Message, spec.Enabled);
    }

    private static ExistentialRule? ParseRuleRow(IReadOnlyList<JsonElement> row)
    {
        var ruleName = JsonRows.StringAt(row, 0) ?? "";
        var raw = JsonRows.StringAt(row, 1) ?? "{}";
        using var doc = JsonDocument.Parse(raw);
        var root = doc.RootElement;
        var forEachRoot = root.GetProperty("forEach");
        var existsRoot = root.GetProperty("exists");
        var where = forEachRoot.TryGetProperty("where", out var whereElement) && whereElement.ValueKind == JsonValueKind.Array
            ? whereElement.EnumerateArray()
                .Select(e => new ExistentialWhereCondition(
                    e.GetProperty("attr").GetString() ?? "",
                    e.TryGetProperty("op", out var op) ? op.GetString() ?? "=" : "=",
                    e.GetProperty("value").Clone()))
                .ToArray()
            : [];
        var direction = existsRoot.TryGetProperty("direction", out var directionElement) &&
                        string.Equals(directionElement.GetString(), "in", StringComparison.OrdinalIgnoreCase)
            ? ExistentialDirection.In
            : ExistentialDirection.Out;
        ExistentialMaterializeSpec? materialize = null;
        if (root.TryGetProperty("materialize", out var mat) && mat.ValueKind == JsonValueKind.Object)
        {
            IReadOnlyDictionary<string, JsonElement>? payload = null;
            if (mat.TryGetProperty("payload", out var payloadElement) && payloadElement.ValueKind == JsonValueKind.Object)
            {
                payload = payloadElement.EnumerateObject().ToDictionary(p => p.Name, p => p.Value.Clone(), StringComparer.Ordinal);
            }

            materialize = new ExistentialMaterializeSpec(
                mat.TryGetProperty("labelTemplate", out var label) ? label.GetString() : null,
                payload);
        }

        var mode = string.Equals(JsonRows.StringAt(row, 2), "materialize", StringComparison.OrdinalIgnoreCase)
            ? ExistentialRuleMode.Materialize
            : ExistentialRuleMode.Check;
        return new ExistentialRule(
            ruleName,
            new ExistentialForEachSpec(forEachRoot.GetProperty("type").GetString() ?? "", where),
            new ExistentialExistsSpec(existsRoot.GetProperty("rel").GetString() ?? "", direction, existsRoot.GetProperty("toClass").GetString() ?? ""),
            materialize,
            mode,
            JsonRows.StringAt(row, 3) ?? "",
            JsonRows.BoolAt(row, 4));
    }
}
