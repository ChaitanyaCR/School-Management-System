using SchoolMs.SharedKernel;

namespace SchoolMs.Infrastructure.Tenancy;

/// <summary>Scoped holder of the current tenant. Set once per scope by the tenant middleware (or a background job).</summary>
public sealed class TenantContext : ITenantContext
{
    public Guid? OrganisationId { get; private set; }

    public string? Slug { get; private set; }

    public void SetTenant(Guid organisationId, string slug)
    {
        if (OrganisationId is not null && OrganisationId != organisationId)
        {
            throw new InvalidOperationException("The tenant of a scope cannot be changed once set.");
        }

        OrganisationId = organisationId;
        Slug = slug;
    }
}
