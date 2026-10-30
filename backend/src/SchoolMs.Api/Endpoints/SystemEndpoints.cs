using System.Reflection;
using Microsoft.Extensions.Options;
using SchoolMs.Infrastructure.Configuration;

namespace SchoolMs.Api.Endpoints;

/// <summary>Public, non-sensitive information the UI needs before login (branding, version).</summary>
public sealed record SystemInfoResponse(string ProductName, string Version, string Environment);

internal static class SystemEndpoints
{
    private static readonly string Version =
        typeof(SystemEndpoints).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
            .Split('+')[0] ?? "0.0.0";

    public static IEndpointRouteBuilder MapSystemEndpoints(this IEndpointRouteBuilder api)
    {
        api.MapGet("/system/info", (IOptions<AppOptions> options, IHostEnvironment environment) =>
                TypedResults.Ok(new SystemInfoResponse(options.Value.ProductName, Version, environment.EnvironmentName)))
            .AllowAnonymous()
            .WithName("GetSystemInfo")
            .WithTags("system")
            .WithSummary("Product name, version and environment.");

        return api;
    }
}
