using System.Reflection;
using Scalar.AspNetCore;
using SchoolMs.Api;
using SchoolMs.Api.Configuration;
using SchoolMs.Api.Endpoints;
using SchoolMs.Infrastructure.Modules;
using SchoolMs.Infrastructure.Observability;
using SchoolMs.Infrastructure.Persistence;
using Serilog;
using Serilog.Events;

var builder = WebApplication.CreateBuilder(args);

if (Assembly.GetEntryAssembly()?.GetName().Name == "GetDocument.Insider")
{
    // Build-time OpenAPI generation starts the app without real configuration.
    // A placeholder satisfies startup validation; no connection is ever opened.
    builder.Configuration["ConnectionStrings:Database"] = "Host=openapi-build-time-generation";
}

builder.AddObservability(serviceName: "schoolms-api");
builder.Services.AddAppOptions(builder.Configuration);
builder.Services.AddPersistence(builder.Configuration);
builder.Services.AddProblemDetails();
builder.Services.AddOpenApi("v1", options => options.AddDocumentTransformer((document, _, _) =>
{
    document.Info.Title = "School Management System API";
    return Task.CompletedTask;
}));
builder.Services.AddModules(builder.Configuration, AppModules.All);

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseSerilogRequestLogging(options => options.GetLevel = (context, _, exception) =>
    exception is not null || context.Response.StatusCode >= 500 ? LogEventLevel.Error
    : context.Request.Path.StartsWithSegments("/health") ? LogEventLevel.Verbose
    : LogEventLevel.Information);

if (app.Configuration.GetValue<bool>($"{AppOptions.SectionName}:{nameof(AppOptions.ExposeApiDocs)}"))
{
    app.MapOpenApi();
    app.MapScalarApiReference("/api/docs");
}

app.MapHealthEndpoints();

var api = app.MapGroup("/api/v1");
api.MapSystemEndpoints();
api.MapModules(AppModules.All);

await app.RunAsync();
