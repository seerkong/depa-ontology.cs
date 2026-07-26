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
                    new { kind = "addType", typeName = "ExampleServerEmployee", description = "Example server employee" }
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
            action = "read"
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
