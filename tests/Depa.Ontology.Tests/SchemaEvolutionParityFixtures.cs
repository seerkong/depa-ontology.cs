using System.Text.Json;
using Depa.Cozo;
using Depa.Ontology;
using Depa.Ontology.Contracts.Models;
using Depa.Ontology.Inputs;

internal static class SchemaEvolutionParityFixtures
{
    public static async Task RunAsync()
    {
        AssertV2ContractShape();
        await AssertV2AtomicMigrationAndRollbackAsync();
        await AssertV2InitializationAndKeyedDiffAsync();
        await CharacterizeStrictMigrationGapAsync();
        await CharacterizeMigrationAtomicityGapAsync();
        await CharacterizeStrictRollbackGapAsync();
        await CharacterizeLegacyInitializationGapAsync();
        await CharacterizeBroadDiffGapAsync();
    }

    private static void AssertV2ContractShape()
    {
        Require(new SchemaMigrationV2Options().Strict,
            "V2 migration options must make strict preflight the immutable default");
        Require(new SchemaRollbackV2Options().Strict && !new SchemaRollbackV2Options().Force,
            "V2 rollback options must be strict by default without implicitly forcing execution");
        Require(new InitializeSchemaV2Input().EffectiveOptions.LegacyHandling == SchemaLegacyHandling.DetectOnly,
            "V2 initialization must default to non-mutating legacy detection");
        Require((new InitializeSchemaV2Input() with
        {
            Options = new SchemaInitializationOptions(SchemaLegacyHandling.Upgrade),
        }).EffectiveOptions.LegacyHandling == SchemaLegacyHandling.Upgrade,
            "V2 initialization options must remain correct when callers use record with-expressions");

        using var source = JsonDocument.Parse("{\"kind\":\"addClass\",\"className\":\"ContractClass\"}");
        var migration = new SchemaMigrationV2Input("schema:contract:v2", 1, 2, [source.RootElement]);
        var diff = new SchemaKeyedDiff(1, 2,
        [
            KeyValuePair.Create("om_class_def", new SchemaDefinitionDiff(
                added: [KeyValuePair.Create("ContractClass", source.RootElement)])),
        ]);
        var result = new SchemaMigrationV2Result("schema:contract:v2", 1, 2, applied: false, strict: true,
            diagnostics: [new SchemaDiagnostic("preflight_failed", SchemaDiagnosticSeverity.Error, "rejected")],
            diff: diff);

        Require(migration.Steps.Length == 1
                && migration.Steps[0].GetProperty("className").GetString() == "ContractClass"
                && diff.Definitions["om_class_def"].Added.ContainsKey("ContractClass")
                && result.Diagnostics.Length == 1,
            "V2 contracts must expose copied immutable steps, keyed definition diffs, and structured diagnostics");
    }

    private static async Task AssertV2AtomicMigrationAndRollbackAsync()
    {
        using var db = new CozoDb(engine: "mem", path: "");
        var om = new CozoOm(db);
        await om.InitSchemaAsync();
        await om.DefineClassAsync("SchemaV2Employee", "schema V2 employee");
        await om.DefineFieldAsync("SchemaV2Employee", "level", OmValueType.String);
        await om.CreateObjectAsync("schema:v2:employee", "SchemaV2Employee", "schema V2 employee");
        await om.SetFieldValueAsync("schema:v2:employee", "level", "not-a-number");

        var strictRejected = await om.ApplySchemaMigrationV2Async(new SchemaMigrationV2Input(
            "schema:v2:strict-rejected",
            1,
            2,
            [
                JsonSerializer.SerializeToElement(new
                {
                    kind = "changeField",
                    className = "SchemaV2Employee",
                    fieldName = "level",
                    valueKind = "Number",
                }),
            ]));
        Require(!strictRejected.Applied
                && strictRejected.Diagnostics.Any(diagnostic => diagnostic.Code == "field_value_kind_incompatible")
                && (await om.GetSchemaStateAsync()).CurrentVersion == 1
                && (await om.GetFieldDefinitionsAsync("SchemaV2Employee"))["level"].ValueKind == OmValueType.String,
            "V2 strict migration must reject incompatible data before any schema write is committed");

        var atomicRejected = await om.ApplySchemaMigrationV2Async(new SchemaMigrationV2Input(
            "schema:v2:atomic-rejected",
            1,
            2,
            [
                JsonSerializer.SerializeToElement(new { kind = "addClass", className = "SchemaV2PartialClass", description = "partial" }),
                JsonSerializer.SerializeToElement(new { kind = "unknownStep" }),
            ],
            options: new SchemaMigrationV2Options(Strict: false)));
        Require(!atomicRejected.Applied
                && !await ClassExistsAsync(db, "SchemaV2PartialClass")
                && (await om.GetSchemaStateAsync()).CurrentVersion == 1,
            "V2 migration must leave no partial type or schema-state write after a rejected later step");

        var requiredRejected = await om.ApplySchemaMigrationV2Async(new SchemaMigrationV2Input(
            "schema:v2:required-rejected",
            1,
            2,
            [
                JsonSerializer.SerializeToElement(new
                {
                    kind = "addField",
                    className = "SchemaV2Employee",
                    fieldName = "requiredLevel",
                    valueKind = "Number",
                    required = true,
                }),
            ]));
        Require(!requiredRejected.Applied
                && requiredRejected.Diagnostics.Any(diagnostic => diagnostic.Code == "required_field_value_missing")
                && !(await om.GetFieldDefinitionsAsync("SchemaV2Employee")).ContainsKey("requiredLevel"),
            "V2 strict migration must reject a new required attribute when existing entities lack its value");

        await om.DefineClassAsync("SchemaV2Rollback", "schema V2 rollback type");
        await om.DefineFieldAsync("SchemaV2Rollback", "rollbackLevel", OmValueType.Number);
        await om.WriteSchemaSnapshotAsync(1, "schema V2 rollback baseline");
        await om.ApplySchemaMigrationAsync(new SchemaMigrationSpec(
            "schema:v2:legacy-loosen",
            1,
            2,
            Strict: false,
            Steps:
            [
                JsonSerializer.SerializeToElement(new
                {
                    kind = "changeField",
                    className = "SchemaV2Rollback",
                    fieldName = "rollbackLevel",
                    valueKind = "Json",
                }),
            ]));
        await om.CreateObjectAsync("schema:v2:rollback", "SchemaV2Rollback", "schema V2 rollback");
        await om.SetFieldValueAsync("schema:v2:rollback", "rollbackLevel", "not-a-number");

        var strictRollback = await om.RollbackSchemaV2Async(new RollbackSchemaV2Input(1));
        Require(!strictRollback.Applied
                && strictRollback.Diagnostics.Any(diagnostic => diagnostic.Code == "rollback_object_invalid")
                && (await om.GetSchemaStateAsync()).CurrentVersion == 2,
            $"V2 strict rollback must abort the restored schema when current data violates its target snapshot; " +
            $"applied={strictRollback.Applied}, state={(await om.GetSchemaStateAsync()).CurrentVersion}, " +
            $"diagnostics={string.Join(" | ", strictRollback.Diagnostics.Select(diagnostic => $"{diagnostic.Code}:{diagnostic.Message}"))}");

        var forcedRollback = await om.RollbackSchemaV2Async(
            new RollbackSchemaV2Input(1, new SchemaRollbackV2Options(Strict: true, Force: true)));
        Require(forcedRollback.Applied
                && forcedRollback.Diagnostics.Any(diagnostic => diagnostic.Code == "rollback_object_invalid")
                && (await om.GetSchemaStateAsync()).CurrentVersion == 1,
            "forced V2 rollback must commit while preserving validation diagnostics");
    }

    private static async Task AssertV2InitializationAndKeyedDiffAsync()
    {
        using (var legacyDb = new CozoDb(engine: "mem", path: ""))
        {
            legacyDb.Run(":create om_field_value {object_id, field_name => value}");
            legacyDb.Run(":create om_relation_link {from_object_id, relation_name, to_object_id => payload}");
            legacyDb.Run(
                "?[object_id, field_name, value] <- [[\"schema:v2:legacy\", \"name\", \"Alice\"]]\n" +
                ":put om_field_value {object_id, field_name => value}");
            legacyDb.Run(
                "?[from_object_id, relation_name, to_object_id, payload] <- [[\"schema:v2:legacy\", \"knows\", \"schema:v2:other\", {}]]\n" +
                ":put om_relation_link {from_object_id, relation_name, to_object_id => payload}");

            var legacyOm = new CozoOm(legacyDb);
            var detected = await legacyOm.InitializeSchemaV2Async();
            Require(!detected.Initialized
                    && detected.LegacySchemaDetected
                    && detected.Diagnostics.Any(diagnostic => diagnostic.Code == "legacy_temporal_relation_detected")
                    && !RelationHasColumn(legacyDb, "om_field_value", "valid_time"),
                "V2 default initialization must only detect a legacy temporal schema without mutating it");

            var upgraded = await legacyOm.InitializeSchemaV2Async(
                new InitializeSchemaV2Input(new SchemaInitializationOptions(SchemaLegacyHandling.Upgrade)));
            Require(upgraded.Initialized
                    && upgraded.LegacySchemaDetected
                    && RelationHasColumn(legacyDb, "om_field_value", "valid_time")
                    && RelationHasColumn(legacyDb, "om_relation_link", "tx_time"),
                "explicit V2 upgrade must create both temporal relation shapes");
            using var restoredRows = legacyDb.Run(
                "?[value] := *om_field_value{ object_id: \"schema:v2:legacy\", field_name: \"name\", value @ \"NOW\" }");
            Require(restoredRows.RootElement.GetProperty("rows").GetArrayLength() == 1,
                "explicit V2 upgrade must preserve legacy property rows at NOW");
        }

        using var diffDb = new CozoDb(engine: "mem", path: "");
        var diffOm = new CozoOm(diffDb);
        await diffOm.InitSchemaAsync();
        await diffOm.DefineClassAsync("SchemaV2DiffBase", "schema V2 keyed diff baseline");
        await diffOm.WriteSchemaSnapshotAsync(1, "schema V2 keyed diff v1");
        await diffOm.DefineClassAsync("SchemaV2DiffAdded", "schema V2 keyed diff added");
        await diffOm.DefineOperationAsync("SchemaV2DiffAdded", "schema_v2_operation", "behavior metadata");
        await diffOm.WriteSchemaSnapshotAsync(2, "schema V2 keyed diff v2");

        var diff = await diffOm.DiffSchemaVersionsV2Async(1, 2);
        Require(diff.Definitions["om_class_def"].Added.Keys.Any(key => key.Contains("SchemaV2DiffAdded", StringComparison.Ordinal))
                && diff.Definitions["om_operation_def"].Added.Keys.Any(key => key.Contains("schema_v2_operation", StringComparison.Ordinal)),
            "V2 keyed diff must preserve deterministic type and behavior-definition changes from the broad C# snapshot");
    }

    private static async Task CharacterizeStrictMigrationGapAsync()
    {
        using var db = new CozoDb(engine: "mem", path: "");
        var om = new CozoOm(db);
        await om.InitSchemaAsync();
        await om.DefineClassAsync("SchemaStrictEmployee", "schema strict employee");
        await om.DefineFieldAsync("SchemaStrictEmployee", "code", OmValueType.String);
        await om.CreateObjectAsync("schema:strict:employee", "SchemaStrictEmployee", "schema strict employee");
        await om.SetFieldValueAsync("schema:strict:employee", "code", "not-a-number");

        var legacyDefault = new SchemaMigrationSpec("schema:strict:default", 1, 2);
        Require(!legacyDefault.Strict,
            "legacy SchemaMigrationSpec must characterize its current permissive default before V2 introduces strict-by-default");

        await om.ApplySchemaMigrationAsync(new SchemaMigrationSpec(
            "schema:strict:ignored",
            1,
            2,
            Strict: true,
            Steps:
            [
                JsonSerializer.SerializeToElement(new
                {
                    kind = "changeField",
                    className = "SchemaStrictEmployee",
                    fieldName = "code",
                    valueKind = "Number",
                }),
            ]));

        var definition = (await om.GetFieldDefinitionsAsync("SchemaStrictEmployee"))["code"];
        Require(definition.ValueKind == OmValueType.Number
                && (await om.GetSchemaStateAsync()).CurrentVersion == 2,
            "current C# migration must characterize that Strict=true does not preflight incompatible stored values");
    }

    private static async Task CharacterizeMigrationAtomicityGapAsync()
    {
        using var db = new CozoDb(engine: "mem", path: "");
        var om = new CozoOm(db);
        await om.InitSchemaAsync();

        await ExpectFailureAsync(
            () => om.ApplySchemaMigrationAsync(new SchemaMigrationSpec(
                "schema:atomic:partial",
                1,
                2,
                Strict: true,
                Steps:
                [
                    JsonSerializer.SerializeToElement(new { kind = "addClass", className = "SchemaPartialClass", description = "partial" }),
                    JsonSerializer.SerializeToElement(new { kind = "unknownStep" }),
                ])),
            "unsupported later migration step must fail");

        Require(await ClassExistsAsync(db, "SchemaPartialClass")
                && (await om.GetSchemaStateAsync()).CurrentVersion == 1,
            "current C# migration must characterize its partial schema write when a later step fails");
    }

    private static async Task CharacterizeStrictRollbackGapAsync()
    {
        using var db = new CozoDb(engine: "mem", path: "");
        var om = new CozoOm(db);
        await om.InitSchemaAsync();
        await om.DefineClassAsync("SchemaRollbackEmployee", "schema rollback employee");
        await om.DefineFieldAsync("SchemaRollbackEmployee", "level", OmValueType.Number, required: true);
        await om.WriteSchemaSnapshotAsync(1, "schema rollback v1");

        await om.ApplySchemaMigrationAsync(new SchemaMigrationSpec(
            "schema:rollback:loosen",
            1,
            2,
            Strict: true,
            Steps:
            [
                JsonSerializer.SerializeToElement(new
                {
                    kind = "changeField",
                    className = "SchemaRollbackEmployee",
                    fieldName = "level",
                    valueKind = "String",
                    required = false,
                }),
            ]));
        await om.CreateObjectAsync("schema:rollback:employee", "SchemaRollbackEmployee", "schema rollback employee");
        await om.SetFieldValueAsync("schema:rollback:employee", "level", "not-a-number");

        await om.RollbackSchemaAsync(1, strict: true);

        Require((await om.GetSchemaStateAsync()).CurrentVersion == 1,
            "current C# rollback must characterize that strict=true does not block incompatible current data");
    }

    private static async Task CharacterizeLegacyInitializationGapAsync()
    {
        using var db = new CozoDb(engine: "mem", path: "");
        db.Run(":create om_field_value {object_id, field_name => value}");
        db.Run(
            "?[object_id, field_name, value] <- [[\"schema:legacy:object\", \"legacy_field\", \"legacy value\"]]\n" +
            ":put om_field_value {object_id, field_name => value}");

        var om = new CozoOm(db);
        await om.InitSchemaAsync();

        using var legacyRows = db.Run(
            "?[value] := *om_field_value{object_id: \"schema:legacy:object\", field_name: \"legacy_field\", value}");
        Require(legacyRows.RootElement.GetProperty("rows").GetArrayLength() == 1,
            "legacy relation rows remain preserved by the current additive initialization path");

        Require(typeof(CozoOm).GetMethod(nameof(CozoOm.InitSchemaAsync))?.ReturnType == typeof(Task),
            "current C# initialization must characterize a void legacy-init surface before V2 adds detect-only diagnostics");
    }

    private static async Task CharacterizeBroadDiffGapAsync()
    {
        using var db = new CozoDb(engine: "mem", path: "");
        var om = new CozoOm(db);
        await om.InitSchemaAsync();
        await om.DefineClassAsync("SchemaDiffBase", "schema diff baseline");
        await om.WriteSchemaSnapshotAsync(1, "schema diff v1");
        await om.DefineClassAsync("SchemaDiffAdded", "schema diff addition");
        await om.DefineOperationAsync("SchemaDiffAdded", "schema_operation", "behavior metadata remains in the C# snapshot");
        await om.WriteSchemaSnapshotAsync(2, "schema diff v2");

        var diff = await om.DiffSchemaVersionsAsync(1, 2);
        Require(diff.Added.TryGetProperty("schema", out _)
                && diff.Removed.TryGetProperty("schema", out _)
                && diff.Changed.TryGetProperty("from", out _)
                && !diff.Changed.TryGetProperty("om_class_def", out _),
            "current C# diff must characterize broad snapshots plus a version-only changed marker before keyed V2 diff is added");
    }

    private static Task<bool> ClassExistsAsync(CozoDb db, string className)
    {
        using var result = db.Run(
            "?[class_name] := *om_class_def{class_name, description: _description, parent_class: _parent_class}, class_name = $class_name",
            new { class_name = className });
        return Task.FromResult(result.RootElement.GetProperty("rows").GetArrayLength() == 1);
    }

    private static bool RelationHasColumn(CozoDb db, string relation, string column)
    {
        using var result = db.Run($"::columns {relation}");
        return result.RootElement.GetProperty("rows").EnumerateArray()
            .Any(row => row[0].ValueKind == JsonValueKind.String && row[0].GetString() == column);
    }

    private static async Task ExpectFailureAsync(Func<Task> operation, string message)
    {
        try
        {
            await operation();
        }
        catch (CozoException)
        {
            return;
        }

        throw new InvalidOperationException(message);
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
