namespace SchoolMs.SharedKernel;

/// <summary>
/// The organisation (tenant) the current request or job runs for. Resolved from the request's subdomain.
/// <see cref="OrganisationId"/> is null for platform-level requests; tenant data is then invisible (fail closed).
/// </summary>
public interface ITenantContext
{
    Guid? OrganisationId { get; }

    string? Slug { get; }
}
