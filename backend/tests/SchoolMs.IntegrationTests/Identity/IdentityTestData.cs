using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using SchoolMs.IntegrationTests.Infrastructure;
using SchoolMs.IntegrationTests.Tenancy;
using SchoolMs.Modules.Identity.Domain;

namespace SchoolMs.IntegrationTests.Identity;

internal sealed record TestUser(Guid Id, string Email, string Password);

internal static class IdentityTestData
{
    public const string Password = "kettle-river-lantern-42";

    public static string UniqueEmail() => $"u{Guid.NewGuid():N}"[..13] + "@school.test";

    /// <summary>Creates an active user with a password, through UserManager in the organisation's scope.</summary>
    public static async Task<TestUser> CreateUserAsync(this ApiFactory factory, TestTenant tenant, string? phoneNumber = null)
    {
        var email = UniqueEmail();
        await using var scope = factory.CreateTenantScope(tenant.Id, tenant.Slug);
        var users = scope.ServiceProvider.GetRequiredService<UserManager<User>>();

        var user = User.Invite(tenant.Id, "Test Teacher", email, phoneNumber, TimeProvider.System).Value;
        user.AcceptInvitation(TimeProvider.System);
        var result = await users.CreateAsync(user, Password);
        result.Succeeded.ShouldBeTrue(string.Join(" ", result.Errors.Select(e => e.Description)));

        return new TestUser(user.Id, email, Password);
    }

    /// <summary>A session client on the organisation's subdomain, signed in as a new active user.</summary>
    public static async Task<(SessionClient Client, TestUser User)> SignedInClientAsync(this ApiFactory factory, TestTenant tenant)
    {
        var user = await factory.CreateUserAsync(tenant);
        var client = factory.CreateSessionClient(tenant.Slug);
        await client.StartAsync();
        (await client.SignInAsync(user.Email, user.Password)).EnsureSuccessStatusCode();
        return (client, user);
    }
}
