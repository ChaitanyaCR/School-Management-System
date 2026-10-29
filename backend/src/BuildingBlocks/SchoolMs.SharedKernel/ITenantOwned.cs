namespace SchoolMs.SharedKernel;

/// <summary>
/// Marks an entity as belonging to one organisation (tenant).
/// Every such entity is scoped by an EF Core query filter and a PostgreSQL RLS policy (ADR-0002).
/// </summary>
public interface ITenantOwned
{
    Guid OrganisationId { get; }
}
