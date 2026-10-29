using Microsoft.EntityFrameworkCore;
using SchoolMs.Infrastructure.Tenancy;
using SchoolMs.Modules.Tenancy.Domain;

namespace SchoolMs.Modules.Tenancy.Infrastructure;

internal sealed class TenantLookup(TenancyDbContext db) : ITenantLookup
{
    public Task<TenantInfo?> FindBySlugAsync(string slug, CancellationToken cancellationToken) =>
        db.Organisations
            .AsNoTracking()
            .Where(o => o.Slug == slug)
            .Select(o => new TenantInfo(o.Id, o.Slug, o.Name, o.Status == OrganisationStatus.Active))
            .SingleOrDefaultAsync(cancellationToken);
}
