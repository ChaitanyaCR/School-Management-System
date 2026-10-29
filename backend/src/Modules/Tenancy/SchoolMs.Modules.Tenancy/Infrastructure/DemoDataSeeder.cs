using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SchoolMs.Infrastructure.Persistence;
using SchoolMs.Infrastructure.Tenancy;
using SchoolMs.Modules.Tenancy.Domain;

namespace SchoolMs.Modules.Tenancy.Infrastructure;

/// <summary>Creates the "demo" organisation (reachable at demo.localhost) with two branches. Idempotent.</summary>
internal sealed partial class DemoDataSeeder(
    TenancyDbContext db,
    TenantContext tenantContext,
    TimeProvider clock,
    ILogger<DemoDataSeeder> logger) : IDataSeeder
{
    public const string DemoSlug = "demo";

    public async Task SeedAsync(CancellationToken cancellationToken)
    {
        var organisation = await db.Organisations.SingleOrDefaultAsync(o => o.Slug == DemoSlug, cancellationToken);
        if (organisation is null)
        {
            organisation = Organisation.Create("Demo School Group", DemoSlug, clock).Value;
            db.Organisations.Add(organisation);
            await db.SaveChangesAsync(cancellationToken);
            LogCreated(logger, "organisation", organisation.Slug);
        }

        // Branches are tenant-owned: writing them requires the tenant to be set, exactly as in a request.
        tenantContext.SetTenant(organisation.Id, organisation.Slug);

        if (!await db.Branches.AnyAsync(cancellationToken))
        {
            db.Branches.AddRange(
                Branch.Create(organisation.Id, "Demo Public School, North Campus", "NORTH", clock).Value,
                Branch.Create(organisation.Id, "Demo Public School, South Campus", "SOUTH", clock).Value);
            await db.SaveChangesAsync(cancellationToken);
            LogCreated(logger, "branches", organisation.Slug);
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Demo data: created {What} for '{Slug}'")]
    private static partial void LogCreated(ILogger logger, string what, string slug);
}
