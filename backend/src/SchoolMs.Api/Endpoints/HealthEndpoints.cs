using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using SchoolMs.Infrastructure.Health;

namespace SchoolMs.Api.Endpoints;

internal static class HealthEndpoints
{
    /// <summary>
    /// <c>/health/live</c>: the process is up (no dependencies checked).
    /// <c>/health/ready</c>: dependencies such as the database are reachable.
    /// </summary>
    public static IEndpointRouteBuilder MapHealthEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false })
            .ExcludeFromDescription();

        app.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = check => check.Tags.Contains(HealthTags.Ready) })
            .ExcludeFromDescription();

        return app;
    }
}
