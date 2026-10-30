using System.Net;
using SchoolMs.IntegrationTests.Infrastructure;

namespace SchoolMs.IntegrationTests;

[Collection(nameof(ApiCollection))]
public class HealthEndpointTests(ApiFactory factory)
{
    [Fact]
    public async Task Live_ReturnsHealthy()
    {
        using var client = factory.CreateClient();

        var response = await client.GetAsync(new Uri("/health/live", UriKind.Relative), TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)).ShouldBe("Healthy");
    }

    [Fact]
    public async Task Ready_WithDatabaseUp_ReturnsHealthy()
    {
        using var client = factory.CreateClient();

        var response = await client.GetAsync(new Uri("/health/ready", UriKind.Relative), TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Ready_WithDatabaseDown_ReturnsServiceUnavailable()
    {
        // Same configuration as the shared factory, except the database. Port 1 on loopback refuses immediately.
        await using var unreachableDb = factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("ConnectionStrings:Database", "Host=127.0.0.1;Port=1;Timeout=2");
            builder.UseSetting("Jobs:RunServer", "false");
        });
        using var client = unreachableDb.CreateClient();

        var response = await client.GetAsync(new Uri("/health/ready", UriKind.Relative), TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.ServiceUnavailable);
    }
}
