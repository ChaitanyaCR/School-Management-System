namespace SchoolMs.Infrastructure.Tenancy;

/// <summary>Minimal tenant data needed to route a request.</summary>
public sealed record TenantInfo(Guid OrganisationId, string Slug, string Name, bool IsActive);

/// <summary>Finds an organisation by its subdomain slug. Implemented by the Tenancy module.</summary>
public interface ITenantLookup
{
    Task<TenantInfo?> FindBySlugAsync(string slug, CancellationToken cancellationToken);
}
