using System.Net;
using System.Net.Http.Json;
using SchoolMs.IntegrationTests.Infrastructure;
using SchoolMs.Modules.Tenancy.Endpoints;

namespace SchoolMs.IntegrationTests.Tenancy;

/// <summary>Subdomain → organisation routing (PLT-02) and suspension (PLT-04).</summary>
[Collection(nameof(ApiCollection))]
public class TenantResolutionTests(ApiFactory factory)
{
    private const string CurrentPath = "/api/v1/tenancy/current";

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task OrganisationSubdomain_ResolvesThatOrganisation()
    {
        var tenant = await factory.CreateOrganisationAsync();
        using var client = factory.CreateTenantClient(tenant.Slug);

        var current = await client.GetFromJsonAsync<CurrentOrganisationResponse>(CurrentPath, Ct);

        current.ShouldNotBeNull();
        current.Slug.ShouldBe(tenant.Slug);
        current.Name.ShouldBe("Test School Group");
    }

    [Fact]
    public async Task UnknownSubdomain_Returns404()
    {
        using var client = factory.CreateTenantClient(TenancyTestData.UniqueSlug());

        await AssertProblemAsync(client, CurrentPath, HttpStatusCode.NotFound, "tenant.not_found");
    }

    [Fact]
    public async Task SuspendedOrganisation_Returns403()
    {
        var tenant = await factory.CreateOrganisationAsync(suspended: true);
        using var client = factory.CreateTenantClient(tenant.Slug);

        await AssertProblemAsync(client, CurrentPath, HttpStatusCode.Forbidden, "tenant.suspended");
    }

    [Fact]
    public async Task BareBaseDomain_HasNoOrganisation()
    {
        using var client = factory.CreateClient();

        await AssertProblemAsync(client, CurrentPath, HttpStatusCode.NotFound, "tenant.none");
    }

    [Theory]
    [InlineData("http://a.b.localhost")]
    [InlineData("http://evil.example.com")]
    public async Task HostOutsideTheBaseDomain_Returns404(string baseAddress)
    {
        using var client = factory.CreateClient(new() { BaseAddress = new Uri(baseAddress) });

        await AssertProblemAsync(client, CurrentPath, HttpStatusCode.NotFound, "tenant.unknown_host");
    }

    [Fact]
    public async Task HealthEndpoints_IgnoreTheHost()
    {
        using var client = factory.CreateTenantClient(TenancyTestData.UniqueSlug());

        var response = await client.GetAsync(new Uri("/health/live", UriKind.Relative), Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    private static async Task AssertProblemAsync(HttpClient client, string path, HttpStatusCode status, string code)
    {
        var response = await client.GetAsync(new Uri(path, UriKind.Relative), Ct);

        response.StatusCode.ShouldBe(status);
        var problem = await response.Content.ReadFromJsonAsync<Dictionary<string, object>>(Ct);
        problem.ShouldNotBeNull();
        problem["code"].ToString().ShouldBe(code);
    }
}
