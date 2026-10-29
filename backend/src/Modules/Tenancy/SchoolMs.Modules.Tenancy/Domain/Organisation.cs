using SchoolMs.SharedKernel;

namespace SchoolMs.Modules.Tenancy.Domain;

internal enum OrganisationStatus
{
    Active = 1,
    Suspended = 2,
}

/// <summary>
/// A customer of the platform: a school group, or a single school (PLT-01). This is the tenant itself,
/// so it is not <see cref="ITenantOwned"/>; only platform administrators may change it.
/// </summary>
internal sealed class Organisation : Entity<Guid>
{
    private Organisation(Guid id, string name, string slug, DateTimeOffset createdAt)
        : base(id)
    {
        Name = name;
        Slug = slug;
        Status = OrganisationStatus.Active;
        CreatedAt = createdAt;
    }

    private Organisation()
    {
    }

    public string Name { get; private set; } = string.Empty;

    public string Slug { get; private set; } = string.Empty;

    public OrganisationStatus Status { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public bool IsActive => Status == OrganisationStatus.Active;

    public static Result<Organisation> Create(string name, string slug, TimeProvider clock)
    {
        var trimmedName = name.Trim();
        if (trimmedName.Length is < 2 or > 200)
        {
            return TenancyErrors.InvalidName;
        }

        var normalisedSlug = TenantSlug.Normalise(slug);
        if (normalisedSlug.IsFailure)
        {
            return normalisedSlug.Error;
        }

        return new Organisation(Guid.CreateVersion7(), trimmedName, normalisedSlug.Value, clock.GetUtcNow());
    }

    /// <summary>Suspended organisations cannot be reached at all (PLT-04).</summary>
    public void Suspend() => Status = OrganisationStatus.Suspended;

    public void Activate() => Status = OrganisationStatus.Active;
}
