using System.Text.RegularExpressions;
using SchoolMs.SharedKernel;

namespace SchoolMs.Modules.Tenancy.Domain;

/// <summary>One physical school or campus of an organisation (PLT-05).</summary>
internal sealed partial class Branch : Entity<Guid>, ITenantOwned
{
    private Branch(Guid id, Guid organisationId, string name, string code, DateTimeOffset createdAt)
        : base(id)
    {
        OrganisationId = organisationId;
        Name = name;
        Code = code;
        IsActive = true;
        CreatedAt = createdAt;
    }

    private Branch()
    {
    }

    public Guid OrganisationId { get; private set; }

    public string Name { get; private set; } = string.Empty;

    /// <summary>Short code, unique within the organisation; used in admission numbers (e.g. "NORTH").</summary>
    public string Code { get; private set; } = string.Empty;

    public bool IsActive { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public static Result<Branch> Create(Guid organisationId, string name, string code, TimeProvider clock)
    {
        var trimmedName = name.Trim();
        if (trimmedName.Length is < 2 or > 200)
        {
            return TenancyErrors.InvalidName;
        }

        var normalisedCode = code.Trim().ToUpperInvariant();
        if (!CodePattern().IsMatch(normalisedCode))
        {
            return TenancyErrors.InvalidBranchCode;
        }

        return new Branch(Guid.CreateVersion7(), organisationId, trimmedName, normalisedCode, clock.GetUtcNow());
    }

    public void Deactivate() => IsActive = false;

    public void Activate() => IsActive = true;

    [GeneratedRegex("^[A-Z0-9]{2,10}$", RegexOptions.CultureInvariant)]
    private static partial Regex CodePattern();
}
