using System.Net;
using System.Net.Http.Json;
using SchoolMs.IntegrationTests.Infrastructure;
using SchoolMs.IntegrationTests.Tenancy;
using SchoolMs.Modules.Identity.Endpoints;
using static SchoolMs.IntegrationTests.Identity.SignInTests;

namespace SchoolMs.IntegrationTests.Identity;

/// <summary>Invitations and password reset end to end, through the background job and email (IAM-02, IAM-03, NTF-01).</summary>
[Collection(nameof(ApiCollection))]
public class InvitationTests(ApiFactory factory)
{
    private const string InvitationsPath = "/api/v1/identity/invitations";
    private const string NewPassword = "lantern-meadow-copper-17"; // gitleaks:allow (test-only password)

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task InvitedUser_SetsPasswordFromTheEmailLink_AndIsSignedIn()
    {
        var tenant = await factory.CreateOrganisationAsync();
        var (admin, _) = await factory.SignedInClientAsync(tenant);
        using var _ = admin;
        var email = IdentityTestData.UniqueEmail();

        var invited = await admin.PostAsync(InvitationsPath, new InviteUserRequest("Meena Iyer", email, "98765 43210"));
        invited.StatusCode.ShouldBe(HttpStatusCode.Accepted);

        var message = await factory.Emails.WaitForAsync(email, "invited");
        message.ToName.ShouldBe("Meena Iyer");
        message.Subject.ShouldContain("Test School Group");
        var (userId, token, link) = CapturingEmailSender.ReadLink(message);
        link.Host.ShouldBe($"{tenant.Slug}.localhost");
        link.AbsolutePath.ShouldBe("/accept-invite");

        using var invitee = factory.CreateSessionClient(tenant.Slug);
        await invitee.StartAsync();
        var accepted = await invitee.PostAsync(InvitationsPath + "/accept", new AcceptInvitationRequest(userId, token, NewPassword));

        accepted.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await invitee.GetUserAsync())!.Email.ShouldBe(email);

        // The link is single-use.
        await AssertProblemAsync(
            await invitee.PostAsync(InvitationsPath + "/accept", new AcceptInvitationRequest(userId, token, NewPassword)),
            HttpStatusCode.BadRequest,
            "identity.invalid_token");
    }

    [Fact]
    public async Task Accepting_WithAWeakPassword_ExplainsWhy_AndKeepsTheLinkValid()
    {
        var tenant = await factory.CreateOrganisationAsync();
        var (userId, token, email) = await InviteAsync(tenant);
        using var invitee = factory.CreateSessionClient(tenant.Slug);
        await invitee.StartAsync();

        var weak = await invitee.PostAsync(InvitationsPath + "/accept", new AcceptInvitationRequest(userId, token, email));

        weak.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var problem = await weak.Content.ReadFromJsonAsync<ValidationProblem>(Ct);
        problem!.Code.ShouldBe("identity.password_rejected");
        problem.Errors["password"].ShouldContain("Passwords must not contain your email address.");

        (await invitee.PostAsync(InvitationsPath + "/accept", new AcceptInvitationRequest(userId, token, NewPassword)))
            .StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task ReInviting_InvalidatesTheEarlierLink()
    {
        var tenant = await factory.CreateOrganisationAsync();
        var (userId, firstToken, email) = await InviteAsync(tenant);
        var (admin, _) = await factory.SignedInClientAsync(tenant);
        using var _ = admin;

        var sentBefore = factory.Emails.Sent.Count(m => m.ToAddress == email);
        (await admin.PostAsync(InvitationsPath, new InviteUserRequest("Meena Iyer", email))).StatusCode.ShouldBe(HttpStatusCode.Accepted);
        await WaitUntilAsync(() => factory.Emails.Sent.Count(m => m.ToAddress == email) > sentBefore);

        using var invitee = factory.CreateSessionClient(tenant.Slug);
        await invitee.StartAsync();
        await AssertProblemAsync(
            await invitee.PostAsync(InvitationsPath + "/accept", new AcceptInvitationRequest(userId, firstToken, NewPassword)),
            HttpStatusCode.BadRequest,
            "identity.invalid_token");
    }

    [Fact]
    public async Task Inviting_AnExistingActiveUser_IsAConflict()
    {
        var tenant = await factory.CreateOrganisationAsync();
        var (admin, user) = await factory.SignedInClientAsync(tenant);
        using var _ = admin;

        await AssertProblemAsync(
            await admin.PostAsync(InvitationsPath, new InviteUserRequest("Someone Else", user.Email)),
            HttpStatusCode.Conflict,
            "identity.email_taken");
    }

    [Fact]
    public async Task Inviting_RequiresASignedInUser()
    {
        var tenant = await factory.CreateOrganisationAsync();
        using var client = factory.CreateSessionClient(tenant.Slug);
        await client.StartAsync();

        var response = await client.PostAsync(InvitationsPath, new InviteUserRequest("Meena Iyer", IdentityTestData.UniqueEmail()));

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ForgottenPassword_IsResetFromTheEmailLink_Once()
    {
        var tenant = await factory.CreateOrganisationAsync();
        var user = await factory.CreateUserAsync(tenant);
        using var client = factory.CreateSessionClient(tenant.Slug);
        await client.StartAsync();

        (await client.PostAsync("/api/v1/identity/password/forgot", new ForgotPasswordRequest(user.Email)))
            .StatusCode.ShouldBe(HttpStatusCode.Accepted);
        var (userId, token, link) = CapturingEmailSender.ReadLink(await factory.Emails.WaitForAsync(user.Email, "Reset"));
        link.AbsolutePath.ShouldBe("/reset-password");

        var reset = await client.PostAsync("/api/v1/identity/password/reset", new ResetPasswordRequest(userId, token, NewPassword));
        reset.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        await AssertProblemAsync(
            await client.PostAsync("/api/v1/identity/password/reset", new ResetPasswordRequest(userId, token, NewPassword + "x")),
            HttpStatusCode.BadRequest,
            "identity.invalid_token");
        (await client.SignInAsync(user.Email, user.Password)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await client.SignInAsync(user.Email, NewPassword)).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task ForgottenPassword_ForAnUnknownEmail_LooksTheSame_AndSendsNothing()
    {
        var tenant = await factory.CreateOrganisationAsync();
        using var client = factory.CreateSessionClient(tenant.Slug);
        await client.StartAsync();
        var email = IdentityTestData.UniqueEmail();

        var response = await client.PostAsync("/api/v1/identity/password/forgot", new ForgotPasswordRequest(email));

        response.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        await Task.Delay(TimeSpan.FromSeconds(1), Ct);
        factory.Emails.Sent.ShouldNotContain(m => m.ToAddress == email);
    }

    [Fact]
    public async Task ResetToken_OfAnotherOrganisation_IsRejected()
    {
        var home = await factory.CreateOrganisationAsync();
        var other = await factory.CreateOrganisationAsync();
        var user = await factory.CreateUserAsync(home);
        using var homeClient = factory.CreateSessionClient(home.Slug);
        await homeClient.StartAsync();
        await homeClient.PostAsync("/api/v1/identity/password/forgot", new ForgotPasswordRequest(user.Email));
        var (userId, token, _) = CapturingEmailSender.ReadLink(await factory.Emails.WaitForAsync(user.Email, "Reset"));

        using var otherClient = factory.CreateSessionClient(other.Slug);
        await otherClient.StartAsync();

        await AssertProblemAsync(
            await otherClient.PostAsync("/api/v1/identity/password/reset", new ResetPasswordRequest(userId, token, NewPassword)),
            HttpStatusCode.BadRequest,
            "identity.invalid_token");
    }

    private async Task<(Guid UserId, string Token, string Email)> InviteAsync(TestTenant tenant)
    {
        var (admin, _) = await factory.SignedInClientAsync(tenant);
        using var _ = admin;
        var email = IdentityTestData.UniqueEmail();
        (await admin.PostAsync(InvitationsPath, new InviteUserRequest("Meena Iyer", email))).EnsureSuccessStatusCode();
        var (userId, token, _) = CapturingEmailSender.ReadLink(await factory.Emails.WaitForAsync(email, "invited"));
        return (userId, token, email);
    }

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        for (var i = 0; i < 200 && !condition(); i++)
        {
            await Task.Delay(100, Ct);
        }

        condition().ShouldBeTrue();
    }

    private sealed record ValidationProblem(string Code, Dictionary<string, string[]> Errors);
}
