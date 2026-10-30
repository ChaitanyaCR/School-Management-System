using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SchoolMs.Infrastructure.Persistence;
using SchoolMs.Infrastructure.Tenancy;
using SchoolMs.Modules.Identity.Domain;

namespace SchoolMs.Modules.Identity.Infrastructure;

/// <summary>
/// Creates sign-in accounts for the demo organisation (seeded by the Tenancy module) with the password from
/// <c>Identity:DemoPassword</c>. Skipped when no password is configured. Idempotent. Synthetic people only, on
/// the reserved <c>.test</c> domain, so no real person's data is ever involved.
/// </summary>
internal sealed partial class DemoUserSeeder(
    ITenantLookup tenants,
    TenantContext tenantContext,
    UserManager<User> users,
    IOptions<IdentityModuleOptions> options,
    TimeProvider clock,
    ILogger<DemoUserSeeder> logger) : IDataSeeder
{
    private const string DemoSlug = "demo";

    private static readonly (string Name, string Email)[] DemoUsers =
    [
        ("Asha Verma", "admin@demo.test"),
        ("Rahul Nair", "teacher@demo.test"),
    ];

    public async Task SeedAsync(CancellationToken cancellationToken)
    {
        var password = options.Value.DemoPassword;
        if (string.IsNullOrEmpty(password))
        {
            LogSkipped(logger);
            return;
        }

        var organisation = await tenants.FindBySlugAsync(DemoSlug, cancellationToken);
        if (organisation is null)
        {
            return;
        }

        tenantContext.SetTenant(organisation.OrganisationId, organisation.Slug);

        foreach (var (name, email) in DemoUsers)
        {
            if (await users.FindByEmailAsync(email) is not null)
            {
                continue;
            }

            var user = User.Invite(organisation.OrganisationId, name, email, phoneNumber: null, clock).Value;
            user.AcceptInvitation(clock);
            var result = await users.CreateAsync(user, password);
            if (!result.Succeeded)
            {
                throw new InvalidOperationException(
                    $"Could not create demo user: {string.Join(" ", result.Errors.Select(e => e.Description))}");
            }

            LogCreated(logger, user.Id);
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Demo data: created user {UserId}")]
    private static partial void LogCreated(ILogger logger, Guid userId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Demo data: no Identity:DemoPassword configured, so no demo users were created")]
    private static partial void LogSkipped(ILogger logger);
}
