using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SchoolMs.Modules.Identity.Domain;
using SchoolMs.SharedKernel;

namespace SchoolMs.Modules.Identity.Infrastructure;

internal static class IdentityClaimTypes
{
    /// <summary>The organisation the session was created in.</summary>
    public const string Organisation = "org";
}

/// <summary>Adds the organisation to the session, so a session can never be used on another organisation's address.</summary>
internal sealed class SessionClaimsPrincipalFactory(UserManager<User> userManager, IOptions<IdentityOptions> options)
    : UserClaimsPrincipalFactory<User>(userManager, options)
{
    protected override async Task<ClaimsIdentity> GenerateClaimsAsync(User user)
    {
        var identity = await base.GenerateClaimsAsync(user);
        identity.AddClaim(new Claim(IdentityClaimTypes.Organisation, user.OrganisationId.ToString()));
        return identity;
    }
}

/// <summary>Only active users may sign in: invited users have no password yet and deactivated users are blocked.</summary>
internal sealed class ActiveUserConfirmation : IUserConfirmation<User>
{
    public Task<bool> IsConfirmedAsync(UserManager<User> manager, User user) => Task.FromResult(user.IsActive);
}

/// <summary>
/// Session cookie behaviour for a JSON API: 401/403 instead of redirects to a login page, and on every request
/// a check that the session belongs to the organisation of the current address, before the periodic security
/// stamp check (which ends sessions after a password reset or deactivation).
/// </summary>
internal sealed class SessionCookieEvents(ITenantContext tenant) : CookieAuthenticationEvents
{
    public override async Task ValidatePrincipal(CookieValidatePrincipalContext context)
    {
        var organisation = context.Principal?.FindFirstValue(IdentityClaimTypes.Organisation);
        if (tenant.OrganisationId is not { } current || organisation != current.ToString())
        {
            context.RejectPrincipal();
            await context.HttpContext.SignOutAsync(IdentityConstants.ApplicationScheme);
            return;
        }

        await SecurityStampValidator.ValidatePrincipalAsync(context);
    }

    public override Task RedirectToLogin(RedirectContext<CookieAuthenticationOptions> context)
    {
        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        return Task.CompletedTask;
    }

    public override Task RedirectToAccessDenied(RedirectContext<CookieAuthenticationOptions> context)
    {
        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        return Task.CompletedTask;
    }

    public override Task RedirectToLogout(RedirectContext<CookieAuthenticationOptions> context) => Task.CompletedTask;

    public override Task RedirectToReturnUrl(RedirectContext<CookieAuthenticationOptions> context) => Task.CompletedTask;
}

/// <summary>
/// Invitation links (IAM-02), valid for 72 hours. Like all Identity tokens they embed the user's security
/// stamp, so a link stops working once it is used (setting the password changes the stamp) or re-sent.
/// </summary>
internal sealed class InvitationTokenProvider(
    IDataProtectionProvider dataProtectionProvider,
    IOptions<InvitationTokenProvider.TokenOptions> options,
    ILogger<DataProtectorTokenProvider<User>> logger)
    : DataProtectorTokenProvider<User>(dataProtectionProvider, options, logger)
{
    public const string ProviderName = "Invitation";
    public const string Purpose = "AcceptInvitation";
    public static readonly TimeSpan Lifespan = TimeSpan.FromHours(72);

    internal sealed class TokenOptions : DataProtectionTokenProviderOptions
    {
        public TokenOptions()
        {
            Name = ProviderName;
            TokenLifespan = Lifespan;
        }
    }
}

/// <summary>Password reset links (IAM-03), valid for 1 hour and single-use (resetting changes the security stamp).</summary>
internal sealed class PasswordResetTokenProvider(
    IDataProtectionProvider dataProtectionProvider,
    IOptions<PasswordResetTokenProvider.TokenOptions> options,
    ILogger<DataProtectorTokenProvider<User>> logger)
    : DataProtectorTokenProvider<User>(dataProtectionProvider, options, logger)
{
    public const string ProviderName = "PasswordReset";
    public static readonly TimeSpan Lifespan = TimeSpan.FromHours(1);

    internal sealed class TokenOptions : DataProtectionTokenProviderOptions
    {
        public TokenOptions()
        {
            Name = ProviderName;
            TokenLifespan = Lifespan;
        }
    }
}
