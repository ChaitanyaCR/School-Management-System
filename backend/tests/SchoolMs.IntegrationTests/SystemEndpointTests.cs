using System.Net;
using System.Net.Http.Json;
using SchoolMs.Api.Endpoints;
using SchoolMs.IntegrationTests.Infrastructure;

namespace SchoolMs.IntegrationTests;

[Collection(nameof(ApiCollection))]
public class SystemEndpointTests(ApiFactory factory)
{
    [Fact]
    public async Task Info_ReturnsProductNameVersionAndEnvironment()
    {
        using var client = factory.CreateClient();

        var info = await client.GetFromJsonAsync<SystemInfoResponse>(
            new Uri("/api/v1/system/info", UriKind.Relative), TestContext.Current.CancellationToken);

        info.ShouldNotBeNull();
        info.ProductName.ShouldBe("School Management System");
        info.Version.ShouldNotBeNullOrWhiteSpace();
        info.Environment.ShouldBe("Testing");
    }

    [Fact]
    public async Task UnknownRoute_ReturnsProblemDetails()
    {
        using var client = factory.CreateClient();

        var response = await client.GetAsync(new Uri("/api/v1/does-not-exist", UriKind.Relative), TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");
    }

    [Fact]
    public async Task ApiDocs_AreNotExposedOutsideDevelopment()
    {
        using var client = factory.CreateClient();

        var response = await client.GetAsync(new Uri("/openapi/v1.json", UriKind.Relative), TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }
}
