using System.Net;
using System.Net.Http.Json;
using SchoolMs.IntegrationTests.Infrastructure;
using SchoolMs.IntegrationTests.Tenancy;
using SchoolMs.Modules.Identity.Endpoints;

namespace SchoolMs.IntegrationTests.Identity;

/// <summary>Sign-in, sessions, lockout and throttling (IAM-01, IAM-05, ADR-0003).</summary>
[Collection(nameof(ApiCollection))]
public class SignInTests(ApiFactory factory)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task SignIn_WithEmail_StartsASession()
    {
        var tenant = await factory.CreateOrganisationAsync();
        var user = await factory.CreateUserAsync(tenant);
        using var client = factory.CreateSessionClient(tenant.Slug);
        (await client.StartAsync()).ShouldBeNull();

        var response = await client.SignInAsync(user.Email.ToUpperInvariant(), user.Password);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await response.Content.ReadFromJsonAsync<SessionResponse>(Ct))!.User!.Id.ShouldBe(user.Id);
        (await client.GetUserAsync())!.Email.ShouldBe(user.Email);
    }

    [Fact]
    public async Task SignIn_WithMobileNumber_InAnyCommonFormat()
    {
        var tenant = await factory.CreateOrganisationAsync();
        var user = await factory.CreateUserAsync(tenant, phoneNumber: "+91 91234 56780");
        using var client = factory.CreateSessionClient(tenant.Slug);
        await client.StartAsync();

        var response = await client.SignInAsync("091234-56780", user.Password);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task SessionCookie_IsHttpOnlyAndSameSiteLax()
    {
        var tenant = await factory.CreateOrganisationAsync();
        var user = await factory.CreateUserAsync(tenant);
        using var client = factory.CreateSessionClient(tenant.Slug);
        await client.StartAsync();

        var response = await client.SignInAsync(user.Email, user.Password);

        var cookie = response.Headers.GetValues("Set-Cookie").Single(c => c.StartsWith("session=", StringComparison.Ordinal));
        cookie.ShouldContain("httponly", Case.Insensitive);
        cookie.ShouldContain("samesite=lax", Case.Insensitive);
    }

    [Fact]
    public async Task ByDefault_CookiesAreSecureAndHostOnly()
    {
        // Real environments keep the default (RequireSecureCookies = true); tests and local HTTP turn it off.
        await using var production = factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("Security:RequireSecureCookies", "true");
            builder.UseSetting("Jobs:RunServer", "false");
        });
        var tenant = await factory.CreateOrganisationAsync();
        using var client = production.CreateClient(new() { BaseAddress = new Uri($"https://{tenant.Slug}.localhost") });

        var response = await client.GetAsync(new Uri(SessionClient.SessionPath, UriKind.Relative), Ct);

        var cookies = response.Headers.GetValues("Set-Cookie").ToList();
        cookies.ShouldContain(c => c.StartsWith("__Host-xsrf=", StringComparison.Ordinal) && c.Contains("secure", StringComparison.OrdinalIgnoreCase));
        cookies.ShouldContain(c => c.StartsWith("XSRF-TOKEN=", StringComparison.Ordinal) && c.Contains("secure", StringComparison.OrdinalIgnoreCase));
        cookies.ShouldAllBe(c => !c.Contains("domain=", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task WrongPasswordAndUnknownAccount_GetTheSameAnswer()
    {
        var tenant = await factory.CreateOrganisationAsync();
        var user = await factory.CreateUserAsync(tenant);
        using var client = factory.CreateSessionClient(tenant.Slug);
        await client.StartAsync();

        await AssertProblemAsync(await client.SignInAsync(user.Email, "not-the-password"), HttpStatusCode.Unauthorized, "identity.invalid_credentials");
        await AssertProblemAsync(await client.SignInAsync("nobody@school.test", user.Password), HttpStatusCode.Unauthorized, "identity.invalid_credentials");
    }

    [Fact]
    public async Task UserOfAnotherOrganisation_CannotSignIn()
    {
        var home = await factory.CreateOrganisationAsync();
        var other = await factory.CreateOrganisationAsync();
        var user = await factory.CreateUserAsync(home);
        using var client = factory.CreateSessionClient(other.Slug);
        await client.StartAsync();

        await AssertProblemAsync(await client.SignInAsync(user.Email, user.Password), HttpStatusCode.Unauthorized, "identity.invalid_credentials");
    }

    [Fact]
    public async Task SessionCookie_IsRejectedOnAnotherOrganisationsAddress()
    {
        var home = await factory.CreateOrganisationAsync();
        var other = await factory.CreateOrganisationAsync();
        var (homeClient, _) = await factory.SignedInClientAsync(home);
        using var _ = homeClient;

        // Replay the session cookie against another organisation's subdomain (as a cookie-tossing attacker would).
        using var otherClient = factory.CreateSessionClient(other.Slug);
        foreach (Cookie cookie in homeClient.Cookies.GetCookies(homeClient.BaseAddress))
        {
            otherClient.Cookies.Add(otherClient.BaseAddress, new Cookie(cookie.Name, cookie.Value));
        }

        (await otherClient.StartAsync()).ShouldBeNull();

        // An endpoint that needs only "a signed-in user" must not accept the foreign session either.
        var response = await otherClient.PostAsync("/api/v1/identity/invitations", new InviteUserRequest("Mallory", IdentityTestData.UniqueEmail()));
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task FiveFailures_LockTheAccount_EvenForTheRightPassword()
    {
        var tenant = await factory.CreateOrganisationAsync();
        var user = await factory.CreateUserAsync(tenant);
        using var client = factory.CreateSessionClient(tenant.Slug);
        await client.StartAsync();

        for (var i = 0; i < 5; i++)
        {
            await client.SignInAsync(user.Email, "not-the-password");
        }

        await AssertProblemAsync(await client.SignInAsync(user.Email, user.Password), HttpStatusCode.Forbidden, "identity.locked_out");
    }

    [Fact]
    public async Task SignOut_EndsTheSession()
    {
        var tenant = await factory.CreateOrganisationAsync();
        var (client, _) = await factory.SignedInClientAsync(tenant);
        using var _ = client;

        var response = await client.Http.DeleteAsync(new Uri(SessionClient.SessionPath, UriKind.Relative), Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await client.GetUserAsync()).ShouldBeNull();
    }

    [Fact]
    public async Task StateChangingRequest_WithoutAntiforgeryToken_IsRejected()
    {
        var tenant = await factory.CreateOrganisationAsync();
        var user = await factory.CreateUserAsync(tenant);
        using var client = factory.CreateTenantClient(tenant.Slug);

        var response = await client.PostAsJsonAsync(
            new Uri(SessionClient.SessionPath, UriKind.Relative),
            new SignInRequest(user.Email, user.Password),
            Ct);

        await AssertProblemAsync(response, HttpStatusCode.BadRequest, "antiforgery.invalid");
    }

    [Fact]
    public async Task RepeatedAttempts_AreThrottled()
    {
        var tenant = await factory.CreateOrganisationAsync();
        using var client = factory.CreateSessionClient(tenant.Slug);
        await client.StartAsync();

        HttpResponseMessage? response = null;
        for (var i = 0; i < 21; i++)
        {
            response = await client.SignInAsync("nobody@school.test", "not-the-password");
        }

        await AssertProblemAsync(response!, HttpStatusCode.TooManyRequests, "rate_limited");
        response!.Headers.RetryAfter.ShouldNotBeNull();
    }

    [Fact]
    public async Task MissingRequiredField_IsABadRequest()
    {
        var tenant = await factory.CreateOrganisationAsync();
        using var client = factory.CreateSessionClient(tenant.Slug);
        await client.StartAsync();

        var response = await client.PostAsync(SessionClient.SessionPath, new { identifier = "someone@school.test" });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    internal static async Task AssertProblemAsync(HttpResponseMessage response, HttpStatusCode status, string code)
    {
        response.StatusCode.ShouldBe(status);
        var problem = await response.Content.ReadFromJsonAsync<Dictionary<string, object>>(Ct);
        problem.ShouldNotBeNull();
        problem["code"].ToString().ShouldBe(code);
    }
}
