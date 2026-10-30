using System.Globalization;
using Hangfire;
using Microsoft.AspNetCore.Identity;
using SchoolMs.Infrastructure.Configuration;
using SchoolMs.Infrastructure.Email;
using SchoolMs.Infrastructure.Tenancy;
using SchoolMs.Modules.Identity.Domain;

namespace SchoolMs.Modules.Identity.Infrastructure.Email;

/// <summary>
/// Sends invitation and password reset emails (NTF-01) in the background. The link token is generated here,
/// at send time, so it never sits in the job queue's storage.
/// </summary>
internal sealed class AccountEmailJob(
    TenantContext tenantContext,
    ITenantLookup tenants,
    UserManager<User> users,
    EmailTemplates templates,
    IEmailSender sender,
    TenantLinks links)
{
    [AutomaticRetry(Attempts = 5)]
    public async Task SendInvitationAsync(TenantRef tenant, Guid userId, CancellationToken cancellationToken)
    {
        var (user, organisationName) = await LoadAsync(tenant, userId, UserStatus.Invited, cancellationToken);
        if (user is null)
        {
            return; // Accepted, removed or the organisation was suspended in the meantime.
        }

        var token = await users.GenerateUserTokenAsync(user, InvitationTokenProvider.ProviderName, InvitationTokenProvider.Purpose);
        await SendAsync(
            user,
            "invitation",
            $"You're invited to {organisationName}",
            new Dictionary<string, object?>
            {
                ["organisation_name"] = organisationName,
                ["link"] = Link(tenant, "/accept-invite", user, token),
                ["valid_hours"] = (int)InvitationTokenProvider.Lifespan.TotalHours,
            },
            cancellationToken);
    }

    [AutomaticRetry(Attempts = 5)]
    public async Task SendPasswordResetAsync(TenantRef tenant, Guid userId, CancellationToken cancellationToken)
    {
        var (user, organisationName) = await LoadAsync(tenant, userId, UserStatus.Active, cancellationToken);
        if (user is null)
        {
            return;
        }

        var token = await users.GeneratePasswordResetTokenAsync(user);
        await SendAsync(
            user,
            "password-reset",
            "Reset your password",
            new Dictionary<string, object?>
            {
                ["organisation_name"] = organisationName,
                ["link"] = Link(tenant, "/reset-password", user, token),
                ["valid_minutes"] = (int)PasswordResetTokenProvider.Lifespan.TotalMinutes,
            },
            cancellationToken);
    }

    private async Task<(User? User, string OrganisationName)> LoadAsync(
        TenantRef tenant,
        Guid userId,
        UserStatus expectedStatus,
        CancellationToken cancellationToken)
    {
        tenantContext.SetTenant(tenant.OrganisationId, tenant.Slug);

        var organisation = await tenants.FindBySlugAsync(tenant.Slug, cancellationToken);
        if (organisation is not { IsActive: true } || organisation.OrganisationId != tenant.OrganisationId)
        {
            return (null, string.Empty);
        }

        var user = await users.FindByIdAsync(userId.ToString());
        return (user?.Status == expectedStatus ? user : null, organisation.Name);
    }

    /// <summary>The user id and token go in the URL fragment, which browsers never send to a server.</summary>
    private string Link(TenantRef tenant, string path, User user, string token) =>
        links.For(tenant.Slug, string.Create(
            CultureInfo.InvariantCulture,
            $"{path}#uid={user.Id}&token={Uri.EscapeDataString(token)}")).ToString();

    private async Task SendAsync(
        User user,
        string template,
        string subject,
        Dictionary<string, object?> model,
        CancellationToken cancellationToken)
    {
        model["name"] = user.FullName;
        var (html, text) = await templates.RenderAsync(typeof(AccountEmailJob).Assembly, template, model);
        await sender.SendAsync(new EmailMessage(user.Email, user.FullName, subject, html, text), cancellationToken);
    }
}
