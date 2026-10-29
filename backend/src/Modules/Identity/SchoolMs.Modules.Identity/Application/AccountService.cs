using Hangfire;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SchoolMs.Infrastructure.Tenancy;
using SchoolMs.Modules.Identity.Domain;
using SchoolMs.Modules.Identity.Infrastructure;
using SchoolMs.Modules.Identity.Infrastructure.Email;
using SchoolMs.Modules.Identity.Infrastructure.Passwords;
using SchoolMs.SharedKernel;

namespace SchoolMs.Modules.Identity.Application;

/// <summary>
/// Sign-in, invitations and password reset (IAM-01…05) for the organisation of the current request.
/// Logs carry user ids, never email addresses or mobile numbers (DPDP data minimisation).
/// </summary>
internal sealed partial class AccountService(
    UserManager<User> users,
    SignInManager<User> signInManager,
    Argon2idPasswordHasher passwordHasher,
    IdentityDbContext db,
    ITenantContext tenant,
    IBackgroundJobClient jobs,
    TimeProvider clock,
    ILogger<AccountService> logger)
{
    /// <summary>"Remember me" keeps the session for 14 days; otherwise it ends after 12 idle hours or when the browser closes.</summary>
    public static readonly TimeSpan RememberMeLifetime = TimeSpan.FromDays(14);

    public async Task<Result<User>> SignInAsync(string identifier, string password, bool rememberMe, CancellationToken cancellationToken)
    {
        var user = password.Length <= PasswordPolicyValidator.MaxLength
            ? await FindByIdentifierAsync(identifier, cancellationToken)
            : null;
        if (user is null)
        {
            passwordHasher.SimulateVerification(password);
            return IdentityErrors.InvalidCredentials;
        }

        var result = await signInManager.CheckPasswordSignInAsync(user, password, lockoutOnFailure: true);
        if (result.IsLockedOut)
        {
            LogLockedOut(logger, user.Id);
            return IdentityErrors.LockedOut;
        }

        if (!result.Succeeded)
        {
            if (result.IsNotAllowed)
            {
                // Inactive accounts are rejected before the password is checked; keep the timing identical.
                passwordHasher.SimulateVerification(password);
            }

            LogFailed(logger, user.Id);
            return IdentityErrors.InvalidCredentials;
        }

        user.RecordSignIn(clock);
        await users.UpdateAsync(user);

        var properties = new AuthenticationProperties
        {
            IsPersistent = rememberMe,
            ExpiresUtc = rememberMe ? clock.GetUtcNow().Add(RememberMeLifetime) : null,
        };
        await signInManager.SignInAsync(user, properties, authenticationMethod: "pwd");
        LogSignedIn(logger, user.Id);
        return user;
    }

    /// <summary>
    /// Creates an invited user and emails them a link to set their password. Inviting someone who has not
    /// accepted yet sends a fresh link and invalidates the earlier one.
    /// </summary>
    public async Task<Result<Guid>> InviteAsync(string fullName, string email, string? phoneNumber, CancellationToken cancellationToken)
    {
        var current = CurrentTenant();
        var invited = User.Invite(current.OrganisationId, fullName, email, phoneNumber, clock);
        if (invited.IsFailure)
        {
            return invited.Error;
        }

        var existing = await users.FindByEmailAsync(invited.Value.Email);
        if (existing is not null)
        {
            if (existing.Status != UserStatus.Invited)
            {
                return IdentityErrors.EmailTaken;
            }

            await users.UpdateSecurityStampAsync(existing);
            EnqueueInvitation(current, existing.Id);
            return existing.Id;
        }

        var user = invited.Value;
        if (user.PhoneNumber is not null && await db.Users.AnyAsync(u => u.PhoneNumber == user.PhoneNumber, cancellationToken))
        {
            return IdentityErrors.PhoneNumberTaken;
        }

        var created = await users.CreateAsync(user);
        if (!created.Succeeded)
        {
            // Another request created the same email or mobile number in the meantime.
            if (created.Errors.Any(e => e.Code == UserStore.DuplicateErrorCode && e.Description.Contains("phone_number", StringComparison.Ordinal)))
            {
                return IdentityErrors.PhoneNumberTaken;
            }

            return created.Errors.Any(e => e.Code is UserStore.DuplicateErrorCode or "DuplicateEmail" or "DuplicateUserName")
                ? IdentityErrors.EmailTaken
                : throw new InvalidOperationException($"Could not create user: {Describe(created)}");
        }

        EnqueueInvitation(current, user.Id);
        LogInvited(logger, user.Id);
        return user.Id;
    }

    /// <summary>Sets the first password from an invitation link, activates the account and signs the user in.</summary>
    public async Task<Result<User>> AcceptInvitationAsync(Guid userId, string token, string password)
    {
        var user = await users.FindByIdAsync(userId.ToString());
        if (user is not { Status: UserStatus.Invited }
            || !await users.VerifyUserTokenAsync(user, InvitationTokenProvider.ProviderName, InvitationTokenProvider.Purpose, token))
        {
            return IdentityErrors.InvalidToken;
        }

        // Saved together with the password; nothing is saved if the password is rejected.
        user.AcceptInvitation(clock);
        var added = await users.AddPasswordAsync(user, password);
        if (!added.Succeeded)
        {
            return IdentityErrors.PasswordRejected(added.Errors.Select(e => e.Description));
        }

        await signInManager.SignInAsync(user, isPersistent: false, authenticationMethod: "pwd");
        LogActivated(logger, user.Id);
        return user;
    }

    /// <summary>
    /// Emails a reset link to an active account, or a fresh invitation to one that was never activated.
    /// Callers always report success, so the response never reveals whether an account exists.
    /// </summary>
    public async Task RequestPasswordResetAsync(string email)
    {
        var current = CurrentTenant();
        var normalised = User.NormaliseEmail(email);
        var user = normalised is null ? null : await users.FindByEmailAsync(normalised);

        switch (user?.Status)
        {
            case UserStatus.Active:
                jobs.Enqueue<AccountEmailJob>(job => job.SendPasswordResetAsync(current, user.Id, CancellationToken.None));
                LogResetRequested(logger, user.Id);
                break;
            case UserStatus.Invited:
                await users.UpdateSecurityStampAsync(user);
                EnqueueInvitation(current, user.Id);
                break;
        }
    }

    /// <summary>Sets a new password from a reset link; this also ends other sessions and clears any lockout.</summary>
    public async Task<Result> ResetPasswordAsync(Guid userId, string token, string password)
    {
        var user = await users.FindByIdAsync(userId.ToString());
        if (user is not { Status: UserStatus.Active })
        {
            return IdentityErrors.InvalidToken;
        }

        var reset = await users.ResetPasswordAsync(user, token, password);
        if (!reset.Succeeded)
        {
            return reset.Errors.Any(e => e.Code == nameof(IdentityErrorDescriber.InvalidToken))
                ? IdentityErrors.InvalidToken
                : IdentityErrors.PasswordRejected(reset.Errors.Select(e => e.Description));
        }

        await users.SetLockoutEndDateAsync(user, null);
        await users.ResetAccessFailedCountAsync(user);
        LogPasswordReset(logger, user.Id);
        return Result.Success();
    }

    private async Task<User?> FindByIdentifierAsync(string identifier, CancellationToken cancellationToken)
    {
        if (identifier.Contains('@', StringComparison.Ordinal))
        {
            var email = User.NormaliseEmail(identifier);
            return email is null ? null : await users.FindByEmailAsync(email);
        }

        var phone = PhoneNumbers.Normalise(identifier);
        return phone.IsSuccess
            ? await db.Users.SingleOrDefaultAsync(u => u.PhoneNumber == phone.Value, cancellationToken)
            : null;
    }

    private TenantRef CurrentTenant() =>
        tenant is { OrganisationId: { } id, Slug: { } slug }
            ? new TenantRef(id, slug)
            : throw new InvalidOperationException("Account operations need an organisation.");

    private void EnqueueInvitation(TenantRef current, Guid userId) =>
        jobs.Enqueue<AccountEmailJob>(job => job.SendInvitationAsync(current, userId, CancellationToken.None));

    private static string Describe(IdentityResult result) => string.Join("; ", result.Errors.Select(e => e.Code));

    [LoggerMessage(Level = LogLevel.Information, Message = "User {UserId} signed in.")]
    private static partial void LogSignedIn(ILogger logger, Guid userId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed sign-in for user {UserId}.")]
    private static partial void LogFailed(ILogger logger, Guid userId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "User {UserId} is locked out after repeated failed sign-ins.")]
    private static partial void LogLockedOut(ILogger logger, Guid userId);

    [LoggerMessage(Level = LogLevel.Information, Message = "User {UserId} invited.")]
    private static partial void LogInvited(ILogger logger, Guid userId);

    [LoggerMessage(Level = LogLevel.Information, Message = "User {UserId} accepted the invitation.")]
    private static partial void LogActivated(ILogger logger, Guid userId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Password reset requested for user {UserId}.")]
    private static partial void LogResetRequested(ILogger logger, Guid userId);

    [LoggerMessage(Level = LogLevel.Information, Message = "User {UserId} reset their password.")]
    private static partial void LogPasswordReset(ILogger logger, Guid userId);
}
