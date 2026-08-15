using System.Net;
using System.Net.Http.Json;
using System.Net.Sockets;
using System.Text.Json;
using Depa.Cozo;
using Depa.Ontology.ExampleServer;

var port = ReserveLoopbackPort();
var baseUrl = $"http://127.0.0.1:{port}";
await using var app = ServerApplication.Build([], baseUrl);
await app.StartAsync();

try
{
    using var client = new HttpClient { BaseAddress = new Uri(baseUrl) };

    using var health = await client.GetFromJsonAsync<JsonDocument>("/health");
    Assert(health?.RootElement.GetProperty("status").GetString() == "ok", "health should return status=ok");
    Assert(health?.RootElement.GetProperty("runtime").GetString() == ".NET", "health should identify the .NET runtime");

    using var demos = await client.GetFromJsonAsync<JsonDocument>("/api/demos");
    var demoIds = demos!.RootElement.GetProperty("demos").EnumerateArray()
        .Select(demo => demo.GetProperty("id").GetString())
        .Where(id => id is not null)
        .ToHashSet(StringComparer.Ordinal);
    foreach (var demoId in new[] { "procurement", "hr", "crm", "resource-graph", "approval-flow", "org-timeline" })
    {
        Assert(demoIds.Contains(demoId), $"/api/demos should contain {demoId}");
    }

    var procurementTables = demos.RootElement.GetProperty("demos").EnumerateArray()
        .Single(demo => demo.GetProperty("id").GetString() == "procurement")
        .GetProperty("tables")
        .EnumerateArray()
        .ToArray();
    var procurementTableNames = procurementTables
        .Select(table => table.GetProperty("name").GetString())
        .ToHashSet(StringComparer.Ordinal);
    foreach (var tableName in new[] { "Class 定义", "Field 定义", "RelationDef 定义", "ComputedProp 定义", "Operation 定义", "Object 数据", "FieldValue 数据", "RelationLink 数据" })
    {
        Assert(procurementTableNames.Contains(tableName), $"/api/demos should teach renamed OM table {tableName}");
    }

    var relationDefColumns = procurementTables
        .Single(table => table.GetProperty("name").GetString() == "RelationDef 定义")
        .GetProperty("columns")
        .EnumerateArray()
        .Select(column => column.GetString())
        .ToArray();
    Assert(relationDefColumns.SequenceEqual(["relationName", "fromClass", "toClass", "directed", "description"]),
        "RelationDef demo table should use relationName/fromClass/toClass columns");

    var relationLinkColumns = procurementTables
        .Single(table => table.GetProperty("name").GetString() == "RelationLink 数据")
        .GetProperty("columns")
        .EnumerateArray()
        .Select(column => column.GetString())
        .ToArray();
    Assert(relationLinkColumns.SequenceEqual(["fromObjectId", "relationName", "toObjectId", "payload"]),
        "RelationLink demo table should use fromObjectId/relationName/toObjectId/payload columns");

    var computedPropColumns = procurementTables
        .Single(table => table.GetProperty("name").GetString() == "ComputedProp 定义")
        .GetProperty("columns")
        .EnumerateArray()
        .Select(column => column.GetString())
        .ToArray();
    Assert(computedPropColumns.SequenceEqual(["className", "computedPropName", "description"]),
        "ComputedProp demo table should use className/computedPropName columns");

    var operationColumns = procurementTables
        .Single(table => table.GetProperty("name").GetString() == "Operation 定义")
        .GetProperty("columns")
        .EnumerateArray()
        .Select(column => column.GetString())
        .ToArray();
    Assert(operationColumns.SequenceEqual(["className", "operationName", "description"]),
        "Operation demo table should use className/operationName columns");

    var approvalTables = demos.RootElement.GetProperty("demos").EnumerateArray()
        .Single(demo => demo.GetProperty("id").GetString() == "approval-flow")
        .GetProperty("tables")
        .EnumerateArray()
        .ToArray();
    Assert(approvalTables.Single(table => table.GetProperty("name").GetString() == "ComputedProp 定义").GetProperty("rows").GetArrayLength() > 0,
        "approval-flow should include a ComputedProp definition row");
    Assert(approvalTables.Single(table => table.GetProperty("name").GetString() == "Operation 定义").GetProperty("rows").GetArrayLength() > 0,
        "approval-flow should include Operation definition rows");

    using var allowedPreflight = new HttpRequestMessage(HttpMethod.Options, "/api/demos");
    allowedPreflight.Headers.Add("Origin", "http://127.0.0.1:4174");
    allowedPreflight.Headers.Add("Access-Control-Request-Method", "GET");
    using var allowedResponse = await client.SendAsync(allowedPreflight);
    Assert(allowedResponse.StatusCode == HttpStatusCode.NoContent, "configured-origin preflight should succeed");
    Assert(allowedResponse.Headers.TryGetValues("Access-Control-Allow-Origin", out var allowedOrigins)
        && allowedOrigins.Single() == "http://127.0.0.1:4174", "configured origin should be echoed");

    using var deniedPreflight = new HttpRequestMessage(HttpMethod.Options, "/api/demos");
    deniedPreflight.Headers.Add("Origin", "https://untrusted.example");
    deniedPreflight.Headers.Add("Access-Control-Request-Method", "GET");
    using var deniedResponse = await client.SendAsync(deniedPreflight);
    Assert(!deniedResponse.Headers.Contains("Access-Control-Allow-Origin"), "unconfigured origins must not be allowed");

    using var unknownDemo = await PostJsonAsync(client, "/api/run", new { demoId = "unknown", queryId = "dslQuery", tables = Array.Empty<object>() });
    Assert(unknownDemo.RootElement.GetProperty("status").GetString() == "error", "unknown demo should return status=error");
    Assert(unknownDemo.RootElement.GetProperty("error").GetString()?.Contains("Unknown demo", StringComparison.Ordinal) == true, "unknown demo should explain the error");

    using var unknownQuery = await PostJsonAsync(client, "/api/run", new { demoId = "procurement", queryId = "unknown", tables = Array.Empty<object>() });
    Assert(unknownQuery.RootElement.GetProperty("status").GetString() == "error", "unknown query should return status=error");
    Assert(unknownQuery.RootElement.GetProperty("error").GetString()?.Contains("Unknown query", StringComparison.Ordinal) == true, "unknown query should explain the error");

    if (CanLoadNativeRuntime())
    {
        foreach (var demo in demos.RootElement.GetProperty("demos").EnumerateArray())
        {
            var demoId = demo.GetProperty("id").GetString()!;
            var queryId = demo.GetProperty("queries").EnumerateArray().First().GetProperty("id").GetString()!;
            using var run = await PostJsonAsync(client, "/api/run", new { demoId, queryId, tables = Array.Empty<object>() });
            Assert(run.RootElement.GetProperty("status").GetString() == "ok", $"default demo query should return status=ok: {demoId}/{queryId}");
            Assert(
                run.RootElement.TryGetProperty("table", out var table)
                    ? table.TryGetProperty("columns", out _) && table.TryGetProperty("rows", out _)
                    : run.RootElement.TryGetProperty("tree", out _) || run.RootElement.TryGetProperty("graph", out _),
                $"default demo result should match browser table/tree/graph shape: {demoId}/{queryId}");
        }

        foreach (var (queryId, resultKey) in new[] { ("impactAnalysis", "graph"), ("ownershipTree", "tree") })
        {
            using var visual = await PostJsonAsync(client, "/api/run", new { demoId = "procurement", queryId, tables = Array.Empty<object>() });
            Assert(visual.RootElement.GetProperty("status").GetString() == "ok", $"{queryId} should succeed");
            Assert(visual.RootElement.TryGetProperty(resultKey, out _), $"{queryId} should return a browser-compatible {resultKey}");
        }

        using var impactGraph = await PostJsonAsync(client, "/api/run", new { demoId = "procurement", queryId = "impactAnalysis", tables = Array.Empty<object>() });
        var graph = impactGraph.RootElement.GetProperty("graph");
        Assert(graph.TryGetProperty("relationLinks", out _), "impactAnalysis graph should expose relationLinks");

        using var initialSchema = await client.GetFromJsonAsync<JsonDocument>("/api/schema/state");
        Assert(initialSchema?.RootElement.GetProperty("currentVersion").GetInt32() == 1,
            "schema state should initialize at version 1");

        using var appliedSchema = await PostJsonAsync(client, "/api/schema/apply", new
        {
            spec = new
            {
                migrationId = "example-server-schema-v2",
                fromVersion = 1,
                toVersion = 2,
                label = "example server contract migration",
                strict = true,
                steps = new[]
                {
                    new { kind = "addClass", className = "ExampleServerEmployee", description = "Example server employee" }
                }
            }
        });
        Assert(appliedSchema.RootElement.GetProperty("ok").GetBoolean(), "schema apply should return ok=true");
        Assert(appliedSchema.RootElement.GetProperty("state").GetProperty("currentVersion").GetInt32() == 2,
            "schema apply should advance the schema state");

        using var schemaDiff = await PostJsonAsync(client, "/api/schema/diff", new { fromVersion = 1, toVersion = 2 });
        Assert(schemaDiff.RootElement.TryGetProperty("diff", out _), "schema diff should return a browser-compatible diff envelope");

        using var integritySeed = await PostJsonAsync(client, "/api/governance/integrity/seed-demo", new { });
        Assert(integritySeed.RootElement.GetProperty("rules").EnumerateArray()
            .Any(rule => rule.GetProperty("ruleName").GetString() == "resource_must_have_owner"),
            "integrity seed should return the demo rule");

        using var integrityViolations = await PostJsonAsync(client, "/api/governance/integrity/check", new { });
        Assert(integrityViolations.RootElement.GetProperty("violations").GetArrayLength() == 2,
            "integrity seed should create two unowned-resource violations");

        using var schemaAfterIntegrityReset = await client.GetFromJsonAsync<JsonDocument>("/api/schema/state");
        Assert(schemaAfterIntegrityReset?.RootElement.GetProperty("currentVersion").GetInt32() == 2,
            "integrity reset must not change the schema domain");

        using var governanceSeed = await PostJsonAsync(client, "/api/governance/seed", new { tables = Array.Empty<object>() });
        Assert(governanceSeed.RootElement.GetProperty("ok").GetBoolean(), "governance seed should return ok=true");

        using var governanceCheck = await PostJsonAsync(client, "/api/governance/checkAccess", new
        {
            subjectId = "u:1",
            resourceId = "r:1",
            operation = "read"
        });
        Assert(governanceCheck.RootElement.TryGetProperty("result", out _),
            "governance check should return its permission result in a browser-compatible envelope");

        using var integrityAfterGovernanceReset = await PostJsonAsync(client, "/api/governance/integrity/check", new { });
        Assert(integrityAfterGovernanceReset.RootElement.GetProperty("violations").GetArrayLength() == 2,
            "governance reset must not change the integrity domain");

        using var integrityApplied = await PostJsonAsync(client, "/api/governance/integrity/apply", new { });
        Assert(integrityApplied.RootElement.GetProperty("ok").GetBoolean(), "integrity apply should return ok=true");
        Assert(integrityApplied.RootElement.GetProperty("result").GetProperty("reachedFixpoint").GetBoolean(),
            "integrity apply should reach a fixpoint for the demo rule");

        using var integrityAfterApply = await PostJsonAsync(client, "/api/governance/integrity/check", new { });
        Assert(integrityAfterApply.RootElement.GetProperty("violations").GetArrayLength() == 0,
            "integrity apply should resolve the demo violations");

        using var rolledBackSchema = await PostJsonAsync(client, "/api/schema/rollback", new { targetVersion = 1, strict = true });
        Assert(rolledBackSchema.RootElement.GetProperty("ok").GetBoolean(), "schema rollback should return ok=true");
        Assert(rolledBackSchema.RootElement.GetProperty("state").GetProperty("currentVersion").GetInt32() == 1,
            "schema rollback should restore version 1");
    }
    else
    {
        Console.WriteLine("Skipping /api/run native contract: cozo_c runtime is unavailable.");
    }

    Console.WriteLine("Example server HTTP contract tests passed.");
}
finally
{
    await app.StopAsync();
}

static int ReserveLoopbackPort()
{
    using var listener = new TcpListener(IPAddress.Loopback, 0);
    listener.Start();
    return ((IPEndPoint)listener.LocalEndpoint).Port;
}

static async Task<JsonDocument> PostJsonAsync(HttpClient client, string path, object body)
{
    using var response = await client.PostAsJsonAsync(path, body);
    response.EnsureSuccessStatusCode();
    return JsonDocument.Parse(await response.Content.ReadAsStringAsync());
}

static bool CanLoadNativeRuntime()
{
    try
    {
        using var database = new CozoDb("mem", "");
        return true;
    }
    catch (DllNotFoundException)
    {
        return false;
    }
    catch (EntryPointNotFoundException)
    {
        return false;
    }
    catch (BadImageFormatException)
    {
        return false;
    }
}

static void Assert(bool condition, string message)
{
    if (!condition)
    {
        throw new InvalidOperationException(message);
    }
}
