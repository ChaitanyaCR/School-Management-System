using System.Reflection;
using Scalar.AspNetCore;
using SchoolMs.Api;
using SchoolMs.Api.Endpoints;
using SchoolMs.Infrastructure.Configuration;
using SchoolMs.Infrastructure.Email;
using SchoolMs.Infrastructure.Jobs;
using SchoolMs.Infrastructure.Modules;
using SchoolMs.Infrastructure.Observability;
using SchoolMs.Infrastructure.Persistence;
using SchoolMs.Infrastructure.Security;
using SchoolMs.Infrastructure.Tenancy;
using SchoolMs.SharedKernel;
using Serilog;
using Serilog.Events;

var builder = WebApplication.CreateBuilder(args);

if (Assembly.GetEntryAssembly()?.GetName().Name == "GetDocument.Insider")
{
    // Build-time OpenAPI generation starts the app without real configuration. Placeholders satisfy startup
    // validation; no connection is ever opened, no email is sent and no background job runs.
    builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
    {
        ["ConnectionStrings:Database"] = "Host=openapi-build-time-generation",
        ["App:PublicOrigin"] = "http://openapi-build-time-generation",
        ["Smtp:Host"] = "openapi-build-time-generation",
        ["Smtp:FromAddress"] = "openapi@build-time.generation",
        ["Jobs:RunServer"] = "false",
    });
}

builder.AddObservability(serviceName: "schoolms-api");
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddAppOptions(builder.Configuration);
builder.Services.AddPersistence(builder.Configuration);
builder.Services.AddTenantResolution(builder.Configuration[$"{AppOptions.SectionName}:{nameof(AppOptions.BaseDomain)}"] ?? "localhost");
builder.Services.AddApiAntiforgery(builder.Configuration);
builder.Services.AddApiRateLimiting();
builder.Services.AddEmail(builder.Configuration);
builder.Services.AddBackgroundJobs(builder.Configuration);

builder.Services.AddAuthorization();

// Reject request bodies with missing or null values for non-nullable properties (400) instead of binding nulls.
// Optional request properties declare a default value. Bad requests answer 400 in every environment
// (by default Development throws instead, which would surface as a 500).
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.RespectNullableAnnotations = true;
    options.SerializerOptions.RespectRequiredConstructorParameters = true;
});
builder.Services.Configure<RouteHandlerOptions>(options => options.ThrowOnBadRequest = false);
builder.Services.AddProblemDetails();
builder.Services.AddOpenApi("v1", options => options.AddDocumentTransformer((document, _, _) =>
{
    document.Info.Title = "School Management System API";
    return Task.CompletedTask;
}));
builder.Services.AddModules(builder.Configuration, AppModules.All);

var app = builder.Build();

if (await HostCommands.TryRunAsync(app, args))
{
    return;
}

app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseSerilogRequestLogging(options =>
{
    options.GetLevel = (context, _, exception) =>
        exception is not null || context.Response.StatusCode >= 500 ? LogEventLevel.Error
        : context.Request.Path.StartsWithSegments("/health") ? LogEventLevel.Verbose
        : LogEventLevel.Information;
    options.EnrichDiagnosticContext = (diagnostics, context) =>
        diagnostics.Set("Tenant", context.RequestServices.GetRequiredService<ITenantContext>().Slug ?? "-");
});
app.UseTenantResolution();
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();
app.UseApiAntiforgery();

if (app.Configuration.GetValue<bool>($"{AppOptions.SectionName}:{nameof(AppOptions.ExposeApiDocs)}"))
{
    app.MapOpenApi();
    app.MapScalarApiReference("/api/docs");
}

app.MapHealthEndpoints();

// Secure by default: every API endpoint requires a signed-in user unless it explicitly allows anonymous access
// (reviewed list in EndpointSecurityTests).
var api = app.MapGroup("/api/v1").RequireAuthorization();
api.MapSystemEndpoints();
api.MapModules(AppModules.All);

await app.RunAsync();
