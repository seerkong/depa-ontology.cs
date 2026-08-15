using System.Text.Json;
using Depa.Ontology.Contracts.Models;
using Depa.Ontology.Inputs;
using Depa.Ontology.Internals;
using Depa.Ontology.Runtime;
using Depa.Ontology.Support;
using Depa.Cozo;

namespace Depa.Ontology.Logic;

public static class SchemaLogic
{
    public static async Task InitSchemaAsync(CozoOmRuntime runtime, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(runtime);

        var creates = new[]
        {
            ":create om_class_def {class_name => description, parent_class}",
            ":create om_mixin_def {name => description}",
            ":create om_class_mixin {class_name, mixin_name}",
            ":create om_operation_def {class_name, operation_name => description}",
            ":create om_mutation_def {class_name, mutation_name => description}",
            ":create om_interceptor_def {class_name, operation_name, phase, seq => description}",
            ":create om_constraint_def {class_name, constraint_name => constraint_kind, message}",
            ":create om_computed_prop_def {class_name, computed_prop_name => description}",
            ":create om_behavior_binding {behavior_kind, owner_class, behavior_name, callback_slot, phase, seq => binding_id}",
            ":create om_perm_operation {operation => description}",
            ":create om_perm_policy {policy_id => effect, operation, resource_class, enabled, description}",
            ":create om_perm_abac_rule {policy_id, left_ref, op, right_ref}",
            ":create om_perm_path_rule {policy_id, path}",
            ":create om_field_def {class_name, field_name => value_kind, required}",
            ":create om_relation_def {relation_name => from_class, to_class, directed}",
            ":create om_object {id => class_name, label}",
            ":create om_field_value {object_id: String, field_name: String, valid_time: Validity => value, tx_time: String}",
            ":create om_relation_link {from_object_id: String, relation_name: String, to_object_id: String, valid_time: Validity => payload, tx_time: String}",
            ":create om_field_desc {class_name, field_name => description}",
            ":create om_relation_desc {relation_name => description}",
            ":create om_schema_state {id => current_version, current_checksum}",
            ":create om_schema_version {version => created_at, label, description, parent_version, checksum}",
            ":create om_schema_migration {migration_id => from_version, to_version, applied_at, applied_by, status, error, summary_json}",
            ":create om_schema_snapshot {version => snapshot_json}",
            ":create om_alias_class {alias => canonical}",
            ":create om_alias_relation {alias => canonical}",
            ":create om_alias_field {class_name, alias_field => canonical_field}",
            ":create om_existential_rule_def {rule_name => spec_json, mode, message, enabled}",
        };

        foreach (var create in creates)
        {
            await LogicSupport.CreateIgnoreConflictAsync(runtime, create, cancellationToken);
        }

        await SeedSchemaStateAsync(runtime, cancellationToken);
    }

    public static async Task<SchemaInitializationResult> InitializeSchemaV2Async(
        CozoOmRuntime runtime,
        InitializeSchemaV2Input? input = null,
        CancellationToken cancellationToken = default)
    {
        var options = input?.EffectiveOptions ?? SchemaInitializationOptions.Default;
        var legacyRelations = await DetectLegacyTemporalRelationsAsync(runtime, cancellationToken);
        if (legacyRelations.Count > 0 && options.LegacyHandling == SchemaLegacyHandling.DetectOnly)
        {
            return new SchemaInitializationResult(
                initialized: false,
                legacyHandling: options.LegacyHandling,
                legacySchemaDetected: true,
                diagnostics: legacyRelations.Select(relation => new SchemaDiagnostic(
                    "legacy_temporal_relation_detected",
                    SchemaDiagnosticSeverity.Warning,
                    $"Relation '{relation}' lacks the V2 temporal fields. Re-run with LegacyHandling=Upgrade to migrate it.",
                    relation)));
        }

        if (legacyRelations.Count == 0)
        {
            await InitSchemaAsync(runtime, cancellationToken);
            return new SchemaInitializationResult(true, options.LegacyHandling, legacySchemaDetected: false);
        }

        await using var transaction = await runtime.Store.BeginTransactionAsync(write: true, cancellationToken);
        var txRuntime = runtime with { Store = transaction };
        await UpgradeLegacyTemporalRelationsAsync(txRuntime, legacyRelations, cancellationToken);
        await InitSchemaAsync(txRuntime, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new SchemaInitializationResult(
            initialized: true,
            legacyHandling: options.LegacyHandling,
            legacySchemaDetected: true,
            diagnostics: legacyRelations.Select(relation => new SchemaDiagnostic(
                "legacy_temporal_relation_upgraded",
                SchemaDiagnosticSeverity.Info,
                $"Relation '{relation}' was upgraded to the V2 temporal shape.",
                relation)));
    }

    public static async Task<SchemaState> GetSchemaStateAsync(CozoOmRuntime runtime, CancellationToken cancellationToken = default)
    {
        var result = await runtime.Store.RunAsync(
            """
            ?[current_version, current_checksum] :=
              *om_schema_state{ id: "default", current_version, current_checksum }
            :limit 1
            """,
            cancellationToken: cancellationToken);
        if (result.Rows.Count == 0)
        {
            throw new CozoException("Schema state not initialized; call InitSchemaAsync first.");
        }

        var row = result.Rows[0];
        return new SchemaState(JsonRows.IntAt(row, 0), JsonRows.StringAt(row, 1));
    }

    public static async Task<IReadOnlyList<SchemaVersion>> ListSchemaVersionsAsync(CozoOmRuntime runtime, CancellationToken cancellationToken = default)
    {
        var result = await runtime.Store.RunAsync(
            """
            ?[version, created_at, label, description, parent_version, checksum] :=
              *om_schema_version{ version, created_at, label, description, parent_version, checksum }
            :sort version
            """,
            cancellationToken: cancellationToken);
        return result.Rows.Select(row => new SchemaVersion(
            JsonRows.IntAt(row, 0),
            JsonRows.StringAt(row, 1) ?? "",
            JsonRows.StringAt(row, 2),
            JsonRows.StringAt(row, 3),
            row[4].ValueKind is JsonValueKind.Null or JsonValueKind.Undefined ? null : row[4].GetInt32(),
            JsonRows.StringAt(row, 5))).ToArray();
    }

    public static async Task<SchemaSnapshot> WriteSchemaSnapshotAsync(
        CozoOmRuntime runtime,
        int version,
        string? label = null,
        string? description = null,
        CancellationToken cancellationToken = default)
    {
        var createdAt = runtime.Options.TimeProvider.GetUtcNow().UtcDateTime.ToString("O");
        var schema = await ReadCurrentSchemaObjectAsync(runtime, cancellationToken);
        var checksum = ComputeChecksum(schema);
        var schemaJson = schema.GetRawText();

        await runtime.Store.RunAsync(
            """
            ?[version, snapshot_json] <- [[$version, $snapshot_json]]
            :put om_schema_snapshot {version => snapshot_json}
            """,
            LogicSupport.Params(("version", version), ("snapshot_json", schemaJson)),
            cancellationToken: cancellationToken);
        await runtime.Store.RunAsync(
            """
            ?[version, created_at, label, description, parent_version, checksum] <- [[$version, $created_at, $label, $description, $parent_version, $checksum]]
            :put om_schema_version {version => created_at, label, description, parent_version, checksum}
            """,
            LogicSupport.Params(
                ("version", version),
                ("created_at", createdAt),
                ("label", label),
                ("description", description),
                ("parent_version", null),
                ("checksum", checksum)),
            cancellationToken: cancellationToken);
        return new SchemaSnapshot(version, createdAt, schema, checksum);
    }

    public static async Task<SchemaSnapshot?> ReadSchemaSnapshotAsync(CozoOmRuntime runtime, int version, CancellationToken cancellationToken = default)
    {
        var result = await runtime.Store.RunAsync(
            """
            ?[snapshot_json] :=
              *om_schema_snapshot{ version: $version, snapshot_json }
            :limit 1
            """,
            LogicSupport.Params(("version", version)),
            cancellationToken: cancellationToken);
        if (result.Rows.Count == 0) return null;

        var raw = JsonRows.StringAt(result.Rows[0], 0) ?? "{}";
        using var doc = JsonDocument.Parse(raw);
        var schema = doc.RootElement.Clone();
        var versionRows = await ListSchemaVersionsAsync(runtime, cancellationToken);
        var meta = versionRows.FirstOrDefault(v => v.Version == version);
        return new SchemaSnapshot(version, meta?.CreatedAt ?? "", schema, meta?.Checksum);
    }

    public static async Task<SchemaDiff> DiffSchemaVersionsAsync(CozoOmRuntime runtime, int fromVersion, int toVersion, CancellationToken cancellationToken = default)
    {
        var from = await ReadSchemaSnapshotAsync(runtime, fromVersion, cancellationToken)
            ?? throw new CozoException($"Missing schema snapshot for version={fromVersion}");
        var to = await ReadSchemaSnapshotAsync(runtime, toVersion, cancellationToken)
            ?? throw new CozoException($"Missing schema snapshot for version={toVersion}");
        return DiffSnapshots(from, to);
    }

    public static async Task<SchemaDiff> DiffCurrentAgainstSnapshotAsync(CozoOmRuntime runtime, int fromVersion, CancellationToken cancellationToken = default)
    {
        var from = await ReadSchemaSnapshotAsync(runtime, fromVersion, cancellationToken)
            ?? throw new CozoException($"Missing schema snapshot for version={fromVersion}");
        var current = new SchemaSnapshot(0, "", await ReadCurrentSchemaObjectAsync(runtime, cancellationToken), null);
        return DiffSnapshots(from, current);
    }

    public static async Task<SchemaKeyedDiff> DiffSchemaVersionsV2Async(
        CozoOmRuntime runtime,
        int fromVersion,
        int toVersion,
        CancellationToken cancellationToken = default)
    {
        var from = await ReadSchemaSnapshotAsync(runtime, fromVersion, cancellationToken)
            ?? throw new CozoException($"Missing schema snapshot for version={fromVersion}");
        var to = await ReadSchemaSnapshotAsync(runtime, toVersion, cancellationToken)
            ?? throw new CozoException($"Missing schema snapshot for version={toVersion}");
        return DiffSnapshotsV2(from, to);
    }

    public static async Task<SchemaKeyedDiff> DiffCurrentAgainstSnapshotV2Async(
        CozoOmRuntime runtime,
        int fromVersion,
        CancellationToken cancellationToken = default)
    {
        var from = await ReadSchemaSnapshotAsync(runtime, fromVersion, cancellationToken)
            ?? throw new CozoException($"Missing schema snapshot for version={fromVersion}");
        var current = new SchemaSnapshot(0, string.Empty, await ReadCurrentSchemaObjectAsync(runtime, cancellationToken), null);
        return DiffSnapshotsV2(from, current);
    }

    public static async Task RollbackSchemaAsync(CozoOmRuntime runtime, int version, bool strict = false, CancellationToken cancellationToken = default)
    {
        var snapshot = await ReadSchemaSnapshotAsync(runtime, version, cancellationToken)
            ?? throw new CozoException($"Missing schema snapshot for version={version}");
        if (!snapshot.Schema.TryGetProperty("schema", out var schemaRoot))
        {
            throw new CozoException($"Schema snapshot version={version} has invalid shape");
        }

        foreach (var spec in RollbackRelations)
        {
            await ClearRelationAsync(runtime, spec, cancellationToken);
        }

        foreach (var spec in RollbackRelations)
        {
            if (!schemaRoot.TryGetProperty(spec.Relation, out var rows) || rows.ValueKind != JsonValueKind.Array) continue;
            foreach (var row in rows.EnumerateArray())
            {
                var cells = row.EnumerateArray().ToArray();
                var entries = new List<(string Key, object? Value)>();
                for (var i = 0; i < spec.Columns.Length && i < cells.Length; i++)
                {
                    entries.Add((spec.Columns[i], JsonSerializer.Deserialize<object?>(cells[i].GetRawText(), OmConvert.JsonOptions)));
                }

                await runtime.Store.RunAsync(
                    CozoScriptBuilder.InputPut(spec.Relation, spec.KeyColumns, spec.Columns.Skip(spec.KeyColumns.Length).ToArray()),
                    LogicSupport.Params(entries.ToArray()),
                    cancellationToken: cancellationToken);
            }
        }

        await runtime.Store.RunAsync(
            """
            ?[id, current_version, current_checksum] <- [["default", $current_version, $current_checksum]]
            :put om_schema_state {id => current_version, current_checksum}
            """,
            LogicSupport.Params(("current_version", version), ("current_checksum", snapshot.Checksum ?? "")),
            cancellationToken: cancellationToken);
    }

    public static async Task<SchemaMigrationResult> ApplySchemaMigrationAsync(
        CozoOmRuntime runtime,
        SchemaMigrationSpec spec,
        CancellationToken cancellationToken = default)
    {
        var migrationId = OmConvert.RequireName(spec.MigrationId, nameof(spec.MigrationId));
        if (spec.FromVersion <= 0) throw new ArgumentException("FromVersion must be positive", nameof(spec));
        if (spec.ToVersion <= 0) throw new ArgumentException("ToVersion must be positive", nameof(spec));
        if (spec.ToVersion == spec.FromVersion) throw new ArgumentException("ToVersion must differ from FromVersion", nameof(spec));

        var state = await GetSchemaStateAsync(runtime, cancellationToken);
        if (state.CurrentVersion != spec.FromVersion)
        {
            throw new CozoException($"Schema currentVersion={state.CurrentVersion} does not match fromVersion={spec.FromVersion}");
        }

        if (await ReadSchemaSnapshotAsync(runtime, spec.FromVersion, cancellationToken) is null)
        {
            await WriteSchemaSnapshotAsync(runtime, spec.FromVersion, $"v{spec.FromVersion}", $"Snapshot before {migrationId}", cancellationToken);
        }

        var steps = spec.Steps ?? [];
        foreach (var step in steps)
        {
            await ApplyMigrationStepAsync(runtime, step, spec.Strict, cancellationToken);
        }

        var snapshot = await WriteSchemaSnapshotAsync(runtime, spec.ToVersion, spec.Label ?? $"v{spec.ToVersion}", spec.Description, cancellationToken);
        var now = runtime.Options.TimeProvider.GetUtcNow().UtcDateTime.ToString("O");
        await runtime.Store.RunAsync(
            """
            ?[version, created_at, label, description, parent_version, checksum] <- [[$version, $created_at, $label, $description, $parent_version, $checksum]]
            :put om_schema_version {version => created_at, label, description, parent_version, checksum}
            """,
            LogicSupport.Params(
                ("version", spec.ToVersion),
                ("created_at", string.IsNullOrWhiteSpace(snapshot.CreatedAt) ? now : snapshot.CreatedAt),
                ("label", spec.Label ?? $"v{spec.ToVersion}"),
                ("description", spec.Description ?? ""),
                ("parent_version", spec.FromVersion),
                ("checksum", snapshot.Checksum ?? "")),
            cancellationToken: cancellationToken);
        await runtime.Store.RunAsync(
            """
            ?[id, current_version, current_checksum] <- [["default", $current_version, $current_checksum]]
            :put om_schema_state {id => current_version, current_checksum}
            """,
            LogicSupport.Params(("current_version", spec.ToVersion), ("current_checksum", snapshot.Checksum ?? "")),
            cancellationToken: cancellationToken);
        await runtime.Store.RunAsync(
            """
            ?[migration_id, from_version, to_version, applied_at, applied_by, status, error, summary_json] <- [[$migration_id, $from_version, $to_version, $applied_at, "", "applied", "", $summary_json]]
            :put om_schema_migration {migration_id => from_version, to_version, applied_at, applied_by, status, error, summary_json}
            """,
            LogicSupport.Params(
                ("migration_id", migrationId),
                ("from_version", spec.FromVersion),
                ("to_version", spec.ToVersion),
                ("applied_at", now),
                ("summary_json", JsonSerializer.SerializeToElement(new
                {
                    migrationId,
                    fromVersion = spec.FromVersion,
                    toVersion = spec.ToVersion,
                    strict = spec.Strict,
                    stepsApplied = steps.Count
                }, OmConvert.JsonOptions))),
            cancellationToken: cancellationToken);

        return new SchemaMigrationResult(migrationId, spec.FromVersion, spec.ToVersion, steps.Count, snapshot.Checksum ?? "");
    }

    public static async Task<SchemaMigrationV2Result> ApplySchemaMigrationV2Async(
        CozoOmRuntime runtime,
        SchemaMigrationV2Input input,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);
        var migrationId = OmConvert.RequireName(input.MigrationId, nameof(input.MigrationId));
        if (input.FromVersion <= 0) throw new ArgumentOutOfRangeException(nameof(input), "FromVersion must be positive.");
        if (input.ToVersion <= 0 || input.ToVersion == input.FromVersion)
        {
            throw new ArgumentOutOfRangeException(nameof(input), "ToVersion must be positive and differ from FromVersion.");
        }

        await using var transaction = await runtime.Store.BeginTransactionAsync(write: true, cancellationToken);
        var txRuntime = runtime with { Store = transaction };
        try
        {
            var state = await GetSchemaStateAsync(txRuntime, cancellationToken);
            if (state.CurrentVersion != input.FromVersion)
            {
                return MigrationRejected(input, "schema_version_mismatch",
                    $"Schema currentVersion={state.CurrentVersion} does not match fromVersion={input.FromVersion}.");
            }

            var diagnostics = await PreflightMigrationAsync(txRuntime, input, cancellationToken);
            if (diagnostics.Any(diagnostic => diagnostic.Severity == SchemaDiagnosticSeverity.Error))
            {
                return new SchemaMigrationV2Result(
                    migrationId,
                    input.FromVersion,
                    input.ToVersion,
                    applied: false,
                    strict: input.Options.Strict,
                    diagnostics: diagnostics);
            }

            var legacyResult = await ApplySchemaMigrationAsync(
                txRuntime,
                new SchemaMigrationSpec(
                    migrationId,
                    input.FromVersion,
                    input.ToVersion,
                    input.Label,
                    input.Description,
                    Strict: false,
                    Steps: input.Steps),
                cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return new SchemaMigrationV2Result(
                legacyResult.MigrationId,
                legacyResult.FromVersion,
                legacyResult.ToVersion,
                applied: true,
                strict: input.Options.Strict,
                diagnostics: diagnostics);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            return MigrationRejected(input, "migration_failed", exception.Message);
        }
    }

    public static async Task<SchemaRollbackV2Result> RollbackSchemaV2Async(
        CozoOmRuntime runtime,
        RollbackSchemaV2Input input,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);
        if (input.TargetVersion <= 0) throw new ArgumentOutOfRangeException(nameof(input), "TargetVersion must be positive.");

        await using var transaction = await runtime.Store.BeginTransactionAsync(write: true, cancellationToken);
        var txRuntime = runtime with { Store = transaction };
        var options = input.EffectiveOptions;
        try
        {
            var state = await GetSchemaStateAsync(txRuntime, cancellationToken);
            if (state.CurrentVersion == input.TargetVersion)
            {
                return new SchemaRollbackV2Result(
                    state.CurrentVersion,
                    input.TargetVersion,
                    applied: true,
                    strict: options.Strict,
                    forced: options.Force);
            }

            await RollbackSchemaAsync(txRuntime, input.TargetVersion, strict: false, cancellationToken);
            var diagnostics = await ValidateRollbackObjectsAsync(txRuntime, cancellationToken);
            if (options.Strict && !options.Force && diagnostics.Length > 0)
            {
                return new SchemaRollbackV2Result(
                    state.CurrentVersion,
                    input.TargetVersion,
                    applied: false,
                    strict: true,
                    forced: false,
                    diagnostics: diagnostics);
            }

            await transaction.CommitAsync(cancellationToken);
            return new SchemaRollbackV2Result(
                state.CurrentVersion,
                input.TargetVersion,
                applied: true,
                strict: options.Strict,
                forced: options.Force,
                diagnostics: diagnostics);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            var state = await GetSchemaStateAsync(runtime, cancellationToken);
            return new SchemaRollbackV2Result(
                state.CurrentVersion,
                input.TargetVersion,
                applied: false,
                strict: options.Strict,
                forced: options.Force,
                diagnostics:
                [
                    new SchemaDiagnostic("rollback_failed", SchemaDiagnosticSeverity.Error, exception.Message),
                ]);
        }
    }

    private static async Task ApplyMigrationStepAsync(
        CozoOmRuntime runtime,
        JsonElement step,
        bool strict,
        CancellationToken cancellationToken)
    {
        var kind = ReadStepString(step, "kind");
        switch (kind)
        {
            case "addClass":
                await ClassLogic.DefineClassAsync(
                    runtime,
                    new DefineClassInput(
                        ReadRequiredStepString(step, "className", "class_name"),
                        ReadStepString(step, "description") ?? ReadRequiredStepString(step, "className", "class_name"),
                        ReadStepString(step, "parentClass", "parent_class")),
                    cancellationToken);
                return;
            case "addField":
                await ClassLogic.DefineFieldAsync(
                    runtime,
                    new DefineFieldInput(
                        ReadRequiredStepString(step, "className", "class_name"),
                        ReadRequiredStepString(step, "fieldName", "field_name"),
                        OmConvert.StoredToValueType(ReadRequiredStepString(step, "valueKind", "value_kind")),
                        ReadStepBool(step, false, "required")),
                    cancellationToken);
                return;
            case "addRelation":
                await ClassLogic.DefineRelationDefAsync(
                    runtime,
                    new DefineRelationDefInput(
                        ReadRequiredStepString(step, "relationName", "relation_name"),
                        ReadRequiredStepString(step, "fromClass", "from_class", "fromClass"),
                        ReadRequiredStepString(step, "toClass", "to_class", "toClass"),
                        ReadStepBool(step, true, "directed")),
                    cancellationToken);
                return;
            case "renameField":
                await RenameFieldAsync(runtime, step, cancellationToken);
                return;
            case "changeField":
                await ChangeFieldAsync(runtime, step, strict, cancellationToken);
                return;
            default:
                throw new CozoException($"Unsupported migration step kind '{kind}'");
        }
    }

    private static async Task RenameFieldAsync(CozoOmRuntime runtime, JsonElement step, CancellationToken cancellationToken)
    {
        var className = await ClassLogic.ResolveClassAsync(runtime, ReadRequiredStepString(step, "className", "class_name"), cancellationToken);
        var fromField = await ClassLogic.ResolveFieldAsync(runtime, className, ReadRequiredStepString(step, "fromField", "from_field"), cancellationToken);
        var toField = OmConvert.RequireName(ReadRequiredStepString(step, "toField", "to_field"), "toField");
        var definitions = await ClassLogic.GetFieldDefinitionsAsync(runtime, className, cancellationToken);
        if (!definitions.TryGetValue(fromField, out var definition))
        {
            throw new CozoException($"Cannot rename missing field '{className}.{fromField}'");
        }

        await ClassLogic.DefineFieldAsync(runtime, new DefineFieldInput(className, toField, definition.ValueType, definition.Required), cancellationToken);
        await ClassLogic.DefineFieldAliasAsync(runtime, className, fromField, toField, cancellationToken);
        await runtime.Store.RunAsync(
            """
            ?[class_name, field_name] <- [[$class_name, $field_name]]
            :rm om_field_def {class_name, field_name}
            """,
            LogicSupport.Params(("class_name", className), ("field_name", fromField)),
            cancellationToken: cancellationToken);
    }

    private static async Task ChangeFieldAsync(
        CozoOmRuntime runtime,
        JsonElement step,
        bool strict,
        CancellationToken cancellationToken)
    {
        var className = await ClassLogic.ResolveClassAsync(runtime, ReadRequiredStepString(step, "className", "class_name"), cancellationToken);
        var fieldName = await ClassLogic.ResolveFieldAsync(runtime, className, ReadRequiredStepString(step, "fieldName", "field_name"), cancellationToken);
        var definitions = await ClassLogic.GetFieldDefinitionsAsync(runtime, className, cancellationToken);
        if (!definitions.TryGetValue(fieldName, out var current))
        {
            throw new CozoException($"Cannot change missing field '{className}.{fieldName}'");
        }

        var valueKind = OmConvert.StoredToValueType(ReadRequiredStepString(step, "valueKind", "value_kind"));
        var required = step.TryGetProperty("required", out _)
            ? ReadStepBool(step, false, "required")
            : current.Required;
        await ClassLogic.DefineFieldAsync(runtime, new DefineFieldInput(className, fieldName, valueKind, required), cancellationToken);
    }

    private static SchemaMigrationV2Result MigrationRejected(
        SchemaMigrationV2Input input,
        string code,
        string message) =>
        new(
            input.MigrationId,
            input.FromVersion,
            input.ToVersion,
            applied: false,
            strict: input.Options.Strict,
            diagnostics:
            [
                new SchemaDiagnostic(code, SchemaDiagnosticSeverity.Error, message),
            ]);

    private static async Task<IReadOnlyList<SchemaDiagnostic>> PreflightMigrationAsync(
        CozoOmRuntime runtime,
        SchemaMigrationV2Input input,
        CancellationToken cancellationToken)
    {
        var diagnostics = new List<SchemaDiagnostic>();
        foreach (var step in input.Steps)
        {
            try
            {
                var kind = ReadStepString(step, "kind");
                if (kind is not ("addClass" or "addField" or "addRelation" or "renameField" or "changeField"))
                {
                    diagnostics.Add(new SchemaDiagnostic(
                        "unsupported_migration_step",
                        SchemaDiagnosticSeverity.Error,
                        $"Unsupported migration step kind '{kind ?? string.Empty}'."));
                    continue;
                }

                if (kind == "changeField" && input.Options.Strict)
                {
                    await PreflightChangeFieldAsync(runtime, step, diagnostics, cancellationToken);
                }
                else if (kind == "addField"
                         && input.Options.Strict
                         && ReadStepBool(step, false, "required"))
                {
                    await PreflightRequiredFieldAsync(runtime, step, diagnostics, cancellationToken);
                }
            }
            catch (Exception exception) when (exception is ArgumentException or CozoException or InvalidOperationException)
            {
                diagnostics.Add(new SchemaDiagnostic("invalid_migration_step", SchemaDiagnosticSeverity.Error, exception.Message));
            }
        }

        return diagnostics;
    }

    private static async Task PreflightChangeFieldAsync(
        CozoOmRuntime runtime,
        JsonElement step,
        ICollection<SchemaDiagnostic> diagnostics,
        CancellationToken cancellationToken)
    {
        var className = await ClassLogic.ResolveClassAsync(runtime, ReadRequiredStepString(step, "className", "class_name"), cancellationToken);
        var fieldName = await ClassLogic.ResolveFieldAsync(runtime, className, ReadRequiredStepString(step, "fieldName", "field_name"), cancellationToken);
        var targetValueType = OmConvert.StoredToValueType(ReadRequiredStepString(step, "valueKind", "value_kind"));
        if (targetValueType == OmValueType.Unknown)
        {
            throw new CozoException($"Unsupported target value kind for '{className}.{fieldName}'.");
        }

        var objects = await runtime.Store.RunAsync(
            """
            ?[id, class_name] :=
              *om_object{ id, class_name, label: _label }
            :sort id
            """,
            cancellationToken: cancellationToken);
        foreach (var row in objects.Rows)
        {
            var objectId = JsonRows.StringAt(row, 0) ?? string.Empty;
            var objectClass = await ClassLogic.ResolveClassAsync(runtime, JsonRows.StringAt(row, 1) ?? string.Empty, cancellationToken);
            var applies = objectClass == className || (await ClassLogic.GetAncestorsAsync(runtime, objectClass, cancellationToken)).Contains(className);
            if (!applies) continue;

            var fieldValues = await ObjectLogic.GetAllFieldValuesAsync(runtime, objectId, cancellationToken);
            if (!fieldValues.TryGetValue(fieldName, out var value))
            {
                if (step.TryGetProperty("required", out var requiredElement) && ReadStepBool(step, false, "required") && requiredElement.ValueKind != JsonValueKind.Null)
                {
                    diagnostics.Add(new SchemaDiagnostic(
                        "required_field_value_missing",
                        SchemaDiagnosticSeverity.Error,
                        $"Object '{objectId}' lacks required field value '{fieldName}'.",
                        "om_field_def",
                        $"{className}:{fieldName}",
                        objectId));
                }

                continue;
            }

            var actualValueType = OmConvert.InferValueType(value);
            if (targetValueType != OmValueType.Json
                && targetValueType != OmValueType.Validity
                && targetValueType != actualValueType)
            {
                diagnostics.Add(new SchemaDiagnostic(
                    "field_value_kind_incompatible",
                    SchemaDiagnosticSeverity.Error,
                    $"Object '{objectId}' field value '{fieldName}' is {actualValueType}, not {targetValueType}.",
                    "om_field_def",
                    $"{className}:{fieldName}",
                    objectId));
            }
        }
    }

    private static async Task PreflightRequiredFieldAsync(
        CozoOmRuntime runtime,
        JsonElement step,
        ICollection<SchemaDiagnostic> diagnostics,
        CancellationToken cancellationToken)
    {
        var requestedClass = ReadRequiredStepString(step, "className", "class_name");
        var className = await TryResolveClassAsync(runtime, requestedClass, cancellationToken) ?? requestedClass;
        var fieldName = ReadRequiredStepString(step, "fieldName", "field_name");
        var objects = await runtime.Store.RunAsync(
            """
            ?[id, class_name] :=
              *om_object{ id, class_name, label: _label }
            :sort id
            """,
            cancellationToken: cancellationToken);
        foreach (var row in objects.Rows)
        {
            var objectId = JsonRows.StringAt(row, 0) ?? string.Empty;
            var objectClass = await ClassLogic.ResolveClassAsync(runtime, JsonRows.StringAt(row, 1) ?? string.Empty, cancellationToken);
            var applies = objectClass == className || (await ClassLogic.GetAncestorsAsync(runtime, objectClass, cancellationToken)).Contains(className);
            if (!applies) continue;

            var fieldValues = await ObjectLogic.GetAllFieldValuesAsync(runtime, objectId, cancellationToken);
            if (!fieldValues.ContainsKey(fieldName))
            {
                diagnostics.Add(new SchemaDiagnostic(
                    "required_field_value_missing",
                    SchemaDiagnosticSeverity.Error,
                    $"Object '{objectId}' lacks new required field value '{fieldName}'.",
                    "om_field_def",
                    $"{className}:{fieldName}",
                    objectId));
            }
        }
    }

    private static async Task<string?> TryResolveClassAsync(
        CozoOmRuntime runtime,
        string className,
        CancellationToken cancellationToken)
    {
        try
        {
            return await ClassLogic.ResolveClassAsync(runtime, className, cancellationToken);
        }
        catch (CozoException)
        {
            return null;
        }
    }

    private static async Task<SchemaDiagnostic[]> ValidateRollbackObjectsAsync(
        CozoOmRuntime runtime,
        CancellationToken cancellationToken)
    {
        var objects = await runtime.Store.RunAsync(
            """
            ?[id] :=
              *om_object{ id, class_name: _class_name, label: _label }
            :sort id
            """,
            cancellationToken: cancellationToken);
        var diagnostics = new List<SchemaDiagnostic>();
        foreach (var row in objects.Rows)
        {
            var objectId = JsonRows.StringAt(row, 0) ?? string.Empty;
            try
            {
                var validation = await ConstraintLogic.ValidateObjectAsync(runtime, objectId, cancellationToken);
                diagnostics.AddRange(validation.Errors.Select(error => new SchemaDiagnostic(
                    "rollback_object_invalid",
                    SchemaDiagnosticSeverity.Error,
                    error,
                    ObjectId: objectId)));
            }
            catch (Exception exception) when (exception is CozoException or InvalidOperationException)
            {
                diagnostics.Add(new SchemaDiagnostic("rollback_object_invalid", SchemaDiagnosticSeverity.Error, exception.Message, ObjectId: objectId));
            }
        }

        return diagnostics.ToArray();
    }

    private static string ReadRequiredStepString(JsonElement step, params string[] names)
    {
        return OmConvert.RequireName(ReadStepString(step, names) ?? "", names[0]);
    }

    private static string? ReadStepString(JsonElement step, params string[] names)
    {
        foreach (var name in names)
        {
            if (step.TryGetProperty(name, out var value))
            {
                return value.ValueKind == JsonValueKind.String ? value.GetString() : value.ToString();
            }
        }

        return null;
    }

    private static bool ReadStepBool(JsonElement step, bool defaultValue, params string[] names)
    {
        foreach (var name in names)
        {
            if (!step.TryGetProperty(name, out var value)) continue;
            return value.ValueKind switch
            {
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                JsonValueKind.String when bool.TryParse(value.GetString(), out var parsed) => parsed,
                _ => defaultValue
            };
        }

        return defaultValue;
    }

    private static async Task SeedSchemaStateAsync(CozoOmRuntime runtime, CancellationToken cancellationToken)
    {
        var stateRows = await runtime.Store.RunAsync(
            """
            ?[current_version] :=
              *om_schema_state{ id: "default", current_version, current_checksum: _c }
            :limit 1
            """,
            cancellationToken: cancellationToken);
        if (stateRows.Rows.Count == 0)
        {
            await runtime.Store.RunAsync(
                """
                ?[id, current_version, current_checksum] <- [["default", 1, ""]]
                :put om_schema_state {id => current_version, current_checksum}
                """,
                cancellationToken: cancellationToken);
        }

        var v1Rows = await runtime.Store.RunAsync(
            """
            ?[created_at] :=
              *om_schema_version{ version: 1, created_at, label: _l, description: _d, parent_version: _p, checksum: _c }
            :limit 1
            """,
            cancellationToken: cancellationToken);
        if (v1Rows.Rows.Count == 0)
        {
            var now = runtime.Options.TimeProvider.GetUtcNow().UtcDateTime.ToString("O");
            await runtime.Store.RunAsync(
                """
                ?[version, created_at, label, description, parent_version, checksum] <- [[1, $created_at, "Initial schema", "Initial OM schema", null, ""]]
                :put om_schema_version {version => created_at, label, description, parent_version, checksum}
                """,
                LogicSupport.Params(("created_at", now)),
                cancellationToken: cancellationToken);
        }
    }

    internal static async Task<JsonElement> ReadCurrentSchemaObjectAsync(CozoOmRuntime runtime, CancellationToken cancellationToken)
    {
        var parts = new Dictionary<string, object?>
        {
            ["om_class_def"] = await ReadRowsAsync(runtime, "om_class_def", "class_name, description, parent_class", cancellationToken),
            ["om_mixin_def"] = await ReadRowsAsync(runtime, "om_mixin_def", "name, description", cancellationToken),
            ["om_class_mixin"] = await ReadRowsAsync(runtime, "om_class_mixin", "class_name, mixin_name", cancellationToken),
            ["om_field_def"] = await ReadRowsAsync(runtime, "om_field_def", "class_name, field_name, value_kind, required", cancellationToken),
            ["om_relation_def"] = await ReadRowsAsync(runtime, "om_relation_def", "relation_name, from_class, to_class, directed", cancellationToken),
            ["om_field_desc"] = await ReadRowsAsync(runtime, "om_field_desc", "class_name, field_name, description", cancellationToken),
            ["om_relation_desc"] = await ReadRowsAsync(runtime, "om_relation_desc", "relation_name, description", cancellationToken),
            ["om_constraint_def"] = await ReadRowsAsync(runtime, "om_constraint_def", "class_name, constraint_name, constraint_kind, message", cancellationToken),
            ["om_computed_prop_def"] = await ReadRowsAsync(runtime, "om_computed_prop_def", "class_name, computed_prop_name, description", cancellationToken),
            ["om_operation_def"] = await ReadRowsAsync(runtime, "om_operation_def", "class_name, operation_name, description", cancellationToken),
            ["om_mutation_def"] = await ReadRowsAsync(runtime, "om_mutation_def", "class_name, mutation_name, description", cancellationToken),
            ["om_interceptor_def"] = await ReadRowsAsync(runtime, "om_interceptor_def", "class_name, operation_name, phase, seq, description", cancellationToken),
            ["om_behavior_binding"] = await ReadRowsAsync(runtime, "om_behavior_binding", "behavior_kind, owner_class, behavior_name, callback_slot, phase, seq, binding_id", cancellationToken),
            ["om_perm_operation"] = await ReadRowsAsync(runtime, "om_perm_operation", "operation, description", cancellationToken),
            ["om_perm_policy"] = await ReadRowsAsync(runtime, "om_perm_policy", "policy_id, effect, operation, resource_class, enabled, description", cancellationToken),
            ["om_perm_abac_rule"] = await ReadRowsAsync(runtime, "om_perm_abac_rule", "policy_id, left_ref, op, right_ref", cancellationToken),
            ["om_perm_path_rule"] = await ReadRowsAsync(runtime, "om_perm_path_rule", "policy_id, path", cancellationToken),
            ["om_alias_class"] = await ReadRowsAsync(runtime, "om_alias_class", "alias, canonical", cancellationToken),
            ["om_alias_relation"] = await ReadRowsAsync(runtime, "om_alias_relation", "alias, canonical", cancellationToken),
            ["om_alias_field"] = await ReadRowsAsync(runtime, "om_alias_field", "class_name, alias_field, canonical_field", cancellationToken),
            ["om_existential_rule_def"] = await ReadRowsAsync(runtime, "om_existential_rule_def", "rule_name, spec_json, mode, message, enabled", cancellationToken),
        };

        return JsonSerializer.SerializeToElement(new { schema = parts }, OmConvert.JsonOptions).Clone();
    }

    private static async Task<IReadOnlyList<IReadOnlyList<JsonElement>>> ReadRowsAsync(
        CozoOmRuntime runtime,
        string relation,
        string columns,
        CancellationToken cancellationToken)
    {
        var fieldAtoms = string.Join(", ", columns.Split(',').Select(c =>
        {
            var trimmed = c.Trim();
            return $"{trimmed}: {trimmed}";
        }));
        var result = await runtime.Store.RunAsync(
            $"?[{columns}] :=\n" +
            $"  *{relation}{{ {fieldAtoms} }}\n" +
            $":sort {columns.Split(',')[0].Trim()}",
            cancellationToken: cancellationToken);
        return result.Rows;
    }

    private static async Task<IReadOnlyList<string>> DetectLegacyTemporalRelationsAsync(
        CozoOmRuntime runtime,
        CancellationToken cancellationToken)
    {
        var relations = await runtime.Store.RunAsync("::relations", cancellationToken: cancellationToken);
        var names = relations.Rows
            .Select(row => row.Count > 0 ? JsonRows.StringAt(row, 0) : null)
            .Where(name => name is "om_field_value" or "om_relation_link")
            .Select(name => name!)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();
        var legacy = new List<string>();
        foreach (var name in names)
        {
            var columns = await runtime.Store.RunAsync($"::columns {name}", cancellationToken: cancellationToken);
            var columnNames = columns.Rows
                .Select(row => row.Count > 0 ? JsonRows.StringAt(row, 0) : null)
                .Where(column => !string.IsNullOrWhiteSpace(column))
                .ToHashSet(StringComparer.Ordinal);
            if (!columnNames.Contains("valid_time") || !columnNames.Contains("tx_time"))
            {
                legacy.Add(name);
            }
        }

        return legacy;
    }

    private static async Task UpgradeLegacyTemporalRelationsAsync(
        CozoOmRuntime runtime,
        IReadOnlyList<string> relations,
        CancellationToken cancellationToken)
    {
        if (relations.Contains("om_field_value", StringComparer.Ordinal))
        {
            await runtime.Store.RunAsync(
                """
                ?[object_id, field_name, valid_time, value, tx_time] :=
                  *om_field_value{ object_id, field_name, value },
                  valid_time = "ASSERT",
                  tx_time = $tx_time
                :replace om_field_value {object_id: String, field_name: String, valid_time: Validity => value, tx_time: String}
                """,
                LogicSupport.Params(("tx_time", runtime.Options.TimeProvider.GetUtcNow().UtcDateTime.ToString("O"))),
                cancellationToken: cancellationToken);
        }

        if (relations.Contains("om_relation_link", StringComparer.Ordinal))
        {
            await runtime.Store.RunAsync(
                """
                ?[from_object_id, relation_name, to_object_id, valid_time, payload, tx_time] :=
                  *om_relation_link{ from_object_id, relation_name, to_object_id, payload },
                  valid_time = "ASSERT",
                  tx_time = $tx_time
                :replace om_relation_link {from_object_id: String, relation_name: String, to_object_id: String, valid_time: Validity => payload, tx_time: String}
                """,
                LogicSupport.Params(("tx_time", runtime.Options.TimeProvider.GetUtcNow().UtcDateTime.ToString("O"))),
                cancellationToken: cancellationToken);
        }
    }

    private static SchemaDiff DiffSnapshots(SchemaSnapshot from, SchemaSnapshot to)
    {
        var fromText = from.Schema.GetRawText();
        var toText = to.Schema.GetRawText();
        var added = fromText == toText ? LogicSupport.EmptyObject() : to.Schema.Clone();
        var removed = fromText == toText ? LogicSupport.EmptyObject() : from.Schema.Clone();
        var changed = fromText == toText ? LogicSupport.EmptyObject() : JsonSerializer.SerializeToElement(new { from = from.Version, to = to.Version }, OmConvert.JsonOptions);
        return new SchemaDiff(from.Version, to.Version, added, removed, changed);
    }

    private static SchemaKeyedDiff DiffSnapshotsV2(SchemaSnapshot from, SchemaSnapshot to)
    {
        var fromTables = ReadSnapshotTables(from.Schema);
        var toTables = ReadSnapshotTables(to.Schema);
        var relationSpecs = RollbackRelations.ToDictionary(spec => spec.Relation, StringComparer.Ordinal);
        var definitions = new List<KeyValuePair<string, SchemaDefinitionDiff>>();
        foreach (var table in fromTables.Keys.Union(toTables.Keys, StringComparer.Ordinal).OrderBy(name => name, StringComparer.Ordinal))
        {
            var keyColumns = relationSpecs.TryGetValue(table, out var spec) ? spec.KeyColumns.Length : 0;
            var before = IndexSnapshotRows(fromTables.GetValueOrDefault(table), keyColumns);
            var after = IndexSnapshotRows(toTables.GetValueOrDefault(table), keyColumns);
            var added = after.Where(pair => !before.ContainsKey(pair.Key));
            var removed = before.Where(pair => !after.ContainsKey(pair.Key));
            var changed = before
                .Where(pair => after.TryGetValue(pair.Key, out var later) && pair.Value.GetRawText() != later.GetRawText())
                .Select(pair => KeyValuePair.Create(pair.Key, new SchemaDefinitionChange(pair.Value, after[pair.Key])));
            definitions.Add(KeyValuePair.Create(table, new SchemaDefinitionDiff(added, removed, changed)));
        }

        return new SchemaKeyedDiff(from.Version, to.Version, definitions);
    }

    private static Dictionary<string, JsonElement> ReadSnapshotTables(JsonElement snapshot)
    {
        var tables = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        if (!snapshot.TryGetProperty("schema", out var schema) || schema.ValueKind != JsonValueKind.Object)
        {
            return tables;
        }

        foreach (var property in schema.EnumerateObject())
        {
            if (property.Value.ValueKind == JsonValueKind.Array)
            {
                tables[property.Name] = property.Value.Clone();
            }
        }

        return tables;
    }

    private static Dictionary<string, JsonElement> IndexSnapshotRows(JsonElement? rows, int keyColumns)
    {
        var indexed = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        if (rows is null || rows.Value.ValueKind != JsonValueKind.Array) return indexed;
        foreach (var row in rows.Value.EnumerateArray())
        {
            var cells = row.ValueKind == JsonValueKind.Array ? row.EnumerateArray().ToArray() : [];
            var key = keyColumns == 0
                ? row.GetRawText()
                : string.Join("\u001f", cells.Take(keyColumns).Select(cell => cell.GetRawText()));
            indexed[key] = row.Clone();
        }

        return indexed;
    }

    private static string ComputeChecksum(JsonElement schema)
    {
        var bytes = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(schema.GetRawText()));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private static async Task ClearRelationAsync(CozoOmRuntime runtime, RelationSnapshotSpec spec, CancellationToken cancellationToken)
    {
        var head = string.Join(", ", spec.KeyColumns);
        var atoms = string.Join(", ", spec.KeyColumns.Select(c => $"{c}: {c}"));
        await runtime.Store.RunAsync(
            $"?[{head}] :=\n" +
            $"  *{spec.Relation}{{ {atoms} }}\n" +
            $":rm {spec.Relation} {{{head}}}",
            cancellationToken: cancellationToken);
    }

    private sealed record RelationSnapshotSpec(string Relation, string[] Columns, string[] KeyColumns);

    private static readonly RelationSnapshotSpec[] RollbackRelations =
    [
        new("om_class_def", ["class_name", "description", "parent_class"], ["class_name"]),
        new("om_mixin_def", ["name", "description"], ["name"]),
        new("om_class_mixin", ["class_name", "mixin_name"], ["class_name", "mixin_name"]),
        new("om_field_def", ["class_name", "field_name", "value_kind", "required"], ["class_name", "field_name"]),
        new("om_relation_def", ["relation_name", "from_class", "to_class", "directed"], ["relation_name"]),
        new("om_field_desc", ["class_name", "field_name", "description"], ["class_name", "field_name"]),
        new("om_relation_desc", ["relation_name", "description"], ["relation_name"]),
        new("om_constraint_def", ["class_name", "constraint_name", "constraint_kind", "message"], ["class_name", "constraint_name"]),
        new("om_computed_prop_def", ["class_name", "computed_prop_name", "description"], ["class_name", "computed_prop_name"]),
        new("om_operation_def", ["class_name", "operation_name", "description"], ["class_name", "operation_name"]),
        new("om_mutation_def", ["class_name", "mutation_name", "description"], ["class_name", "mutation_name"]),
        new("om_interceptor_def", ["class_name", "operation_name", "phase", "seq", "description"], ["class_name", "operation_name", "phase", "seq"]),
        new("om_behavior_binding", ["behavior_kind", "owner_class", "behavior_name", "callback_slot", "phase", "seq", "binding_id"], ["behavior_kind", "owner_class", "behavior_name", "callback_slot", "phase", "seq"]),
        new("om_perm_operation", ["operation", "description"], ["operation"]),
        new("om_perm_policy", ["policy_id", "effect", "operation", "resource_class", "enabled", "description"], ["policy_id"]),
        new("om_perm_abac_rule", ["policy_id", "left_ref", "op", "right_ref"], ["policy_id", "left_ref", "op", "right_ref"]),
        new("om_perm_path_rule", ["policy_id", "path"], ["policy_id", "path"]),
        new("om_alias_class", ["alias", "canonical"], ["alias"]),
        new("om_alias_relation", ["alias", "canonical"], ["alias"]),
        new("om_alias_field", ["class_name", "alias_field", "canonical_field"], ["class_name", "alias_field"]),
        new("om_existential_rule_def", ["rule_name", "spec_json", "mode", "message", "enabled"], ["rule_name"]),
    ];
}
