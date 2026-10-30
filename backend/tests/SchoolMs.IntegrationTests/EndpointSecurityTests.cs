using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using SchoolMs.IntegrationTests.Infrastructure;

namespace SchoolMs.IntegrationTests;

/// <summary>
/// Every API endpoint requires a signed-in user unless it is on this reviewed list. Adding an anonymous
/// endpoint therefore needs a deliberate change here, which shows up in code review.
/// </summary>
[Collection(nameof(ApiCollection))]
public class EndpointSecurityTests(ApiFactory factory)
{
    private static readonly string[] ReviewedAnonymousEndpoints =
    [
        "GET /api/v1/system/info",
        "GET /api/v1/tenancy/current",
        "GET /api/v1/identity/session",
        "POST /api/v1/identity/session",
        "DELETE /api/v1/identity/session",
        "POST /api/v1/identity/invitations/accept",
        "POST /api/v1/identity/password/forgot",
        "POST /api/v1/identity/password/reset",
    ];

    [Fact]
    public void OnlyReviewedApiEndpoints_AllowAnonymousAccess()
    {
        var anonymous = factory.Services.GetRequiredService<EndpointDataSource>().Endpoints
            .OfType<RouteEndpoint>()
            .Where(e => e.RoutePattern.RawText?.StartsWith("/api/", StringComparison.Ordinal) == true)
            .Where(e => e.Metadata.GetMetadata<IAllowAnonymous>() is not null)
            .SelectMany(e => (e.Metadata.GetMetadata<HttpMethodMetadata>()?.HttpMethods ?? ["*"]).Select(m => $"{m} {e.RoutePattern.RawText}"))
            .Order(StringComparer.Ordinal)
            .ToList();

        anonymous.ShouldBe(ReviewedAnonymousEndpoints.Order(StringComparer.Ordinal), ignoreOrder: false);
    }
}
