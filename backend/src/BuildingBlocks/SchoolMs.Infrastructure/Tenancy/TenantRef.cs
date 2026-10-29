namespace SchoolMs.Infrastructure.Tenancy;

/// <summary>
/// Identifies an organisation in a background job's arguments. A job sets it on its scope's
/// <see cref="TenantContext"/> before touching tenant data, so filters and RLS apply as in a request.
/// </summary>
public sealed record TenantRef(Guid OrganisationId, string Slug);
