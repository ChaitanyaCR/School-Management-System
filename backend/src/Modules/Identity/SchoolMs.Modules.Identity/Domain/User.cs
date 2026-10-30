using System.Net.Mail;
using SchoolMs.SharedKernel;

namespace SchoolMs.Modules.Identity.Domain;

internal enum UserStatus
{
    /// <summary>Invited by an administrator; has no password until the invitation is accepted (IAM-02).</summary>
    Invited = 1,
    Active = 2,
    Deactivated = 3,
}

/// <summary>
/// A person who signs in to one organisation (staff in Release 1). Accounts are per organisation: the same email
/// address may hold separate, unrelated accounts in two organisations. The credential fields are maintained by
/// ASP.NET Core Identity through <see cref="Infrastructure.UserStore"/>.
/// </summary>
internal sealed class User : Entity<Guid>, ITenantOwned
{
    public const int MaxEmailLength = 254;

    private User(Guid id, Guid organisationId, string fullName, string email, string? phoneNumber, DateTimeOffset createdAt)
        : base(id)
    {
        OrganisationId = organisationId;
        FullName = fullName;
        Email = email;
        PhoneNumber = phoneNumber;
        Status = UserStatus.Invited;
        CreatedAt = createdAt;
    }

    private User()
    {
    }

    public Guid OrganisationId { get; private set; }

    public string FullName { get; private set; } = string.Empty;

    /// <summary>Lower-case email address; also the Identity user name.</summary>
    public string Email { get; private set; } = string.Empty;

    /// <summary>Indian mobile number in E.164 form (+91XXXXXXXXXX); an alternative sign-in identifier (IAM-01).</summary>
    public string? PhoneNumber { get; private set; }

    public UserStatus Status { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset? ActivatedAt { get; private set; }

    public DateTimeOffset? LastSignInAt { get; private set; }

    public bool IsActive => Status == UserStatus.Active;

    // Maintained by ASP.NET Core Identity (hashing, lockout, token invalidation) through the user store.
    internal string NormalizedEmail { get; set; } = string.Empty;

    internal string? PasswordHash { get; set; }

    internal string SecurityStamp { get; set; } = string.Empty;

    internal string ConcurrencyStamp { get; set; } = Guid.NewGuid().ToString();

    internal bool EmailConfirmed { get; set; }

    internal bool LockoutEnabled { get; set; }

    internal DateTimeOffset? LockoutEnd { get; set; }

    internal int AccessFailedCount { get; set; }

    public static Result<User> Invite(Guid organisationId, string fullName, string email, string? phoneNumber, TimeProvider clock)
    {
        var trimmedName = fullName.Trim();
        if (trimmedName.Length is < 2 or > 200)
        {
            return IdentityErrors.InvalidName;
        }

        var normalisedEmail = NormaliseEmail(email);
        if (normalisedEmail is null)
        {
            return IdentityErrors.InvalidEmail;
        }

        string? normalisedPhone = null;
        if (!string.IsNullOrWhiteSpace(phoneNumber))
        {
            var phone = PhoneNumbers.Normalise(phoneNumber);
            if (phone.IsFailure)
            {
                return phone.Error;
            }

            normalisedPhone = phone.Value;
        }

        return new User(Guid.CreateVersion7(), organisationId, trimmedName, normalisedEmail, normalisedPhone, clock.GetUtcNow());
    }

    /// <summary>Lower-cased address, or null when it is not a single plain address (e.g. "Name &lt;a@b.in&gt;").</summary>
    public static string? NormaliseEmail(string email)
    {
        var trimmed = email.Trim().ToLowerInvariant();
        return trimmed.Length <= MaxEmailLength
            && MailAddress.TryCreate(trimmed, out var address)
            && address.Address == trimmed
            && address.Host.Contains('.', StringComparison.Ordinal)
                ? trimmed
                : null;
    }

    /// <summary>The invitation is accepted together with setting the first password.</summary>
    public void AcceptInvitation(TimeProvider clock)
    {
        Status = UserStatus.Active;
        EmailConfirmed = true;
        ActivatedAt = clock.GetUtcNow();
    }

    public void RecordSignIn(TimeProvider clock) => LastSignInAt = clock.GetUtcNow();
}
