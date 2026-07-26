using System.Text.Json;
using Depa.Ontology.Inputs;

namespace Depa.Ontology.ExampleServer;

public static class ServerApplication
{
    public const string CorsPolicyName = "ExampleBrowser";

    private static readonly string[] DefaultAllowedOrigins =
    [
        "http://localhost:4174",
        "http://127.0.0.1:4174"
    ];

    public static WebApplication Build(string[] args, string? url = null)
    {
        var builder = WebApplication.CreateBuilder(args);
        builder.Services.ConfigureHttpJsonOptions(options =>
        {
            options.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
        });

        var configuredOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins")
            .GetChildren()
            .Select(origin => origin.Value)
            .Where(origin => !string.IsNullOrWhiteSpace(origin))
            .Cast<string>();
        var allowedOrigins = DefaultAllowedOrigins
            .Concat(configuredOrigins)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        builder.Services.AddCors(options => options.AddPolicy(CorsPolicyName, policy => policy
            .WithOrigins(allowedOrigins)
            .AllowAnyHeader()
            .AllowAnyMethod()));

        var app = builder.Build();
        var omState = new OmServerState();
        app.Lifetime.ApplicationStopping.Register(omState.Dispose);
        if (!string.IsNullOrWhiteSpace(url))
        {
            app.Urls.Add(url);
        }

        app.UseCors(CorsPolicyName);
        app.MapGet("/health", () => Results.Ok(new { status = "ok", runtime = ".NET" }));
        app.MapGet("/api/demos", () => Results.Ok(new { demos = DemoCatalog.All }));
        app.MapPost("/api/run", DemoRunner.RunAsync);
        app.MapGet("/api/schema/state", () => OmServerEndpoints.SchemaStateAsync(omState));
        app.MapGet("/api/schema/versions", () => OmServerEndpoints.SchemaVersionsAsync(omState));
        app.MapPost("/api/schema/diff", (SchemaDiffRequest request) => OmServerEndpoints.SchemaDiffAsync(omState, request));
        app.MapPost("/api/schema/apply", (SchemaApplyRequest request) => OmServerEndpoints.SchemaApplyAsync(omState, request));
        app.MapPost("/api/schema/rollback", (SchemaRollbackRequest request) => OmServerEndpoints.SchemaRollbackAsync(omState, request));
        app.MapGet("/api/governance/seed-template", OmServerEndpoints.GovernanceSeedTemplate);
        app.MapPost("/api/governance/seed", () => OmServerEndpoints.GovernanceSeedAsync(omState));
        app.MapPost("/api/governance/checkAccess", (GovernanceAccessRequest request) => OmServerEndpoints.GovernanceCheckAsync(omState, request));
        app.MapPost("/api/governance/explain", (GovernanceAccessRequest request) => OmServerEndpoints.GovernanceCheckAsync(omState, request));
        app.MapPost("/api/governance/integrity/seed-demo", () => OmServerEndpoints.IntegritySeedAsync(omState));
        app.MapGet("/api/governance/integrity/rules", () => OmServerEndpoints.IntegrityRulesAsync(omState));
        app.MapPost("/api/governance/integrity/check", (CheckExistentialRulesInput? request) => OmServerEndpoints.IntegrityCheckAsync(omState, request));
        app.MapPost("/api/governance/integrity/apply", (ApplyExistentialRulesInput? request) => OmServerEndpoints.IntegrityApplyAsync(omState, request));

        return app;
    }
}
