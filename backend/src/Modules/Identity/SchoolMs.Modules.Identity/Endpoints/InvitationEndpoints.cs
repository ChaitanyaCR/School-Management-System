using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Routing;
using SchoolMs.Infrastructure.Http;
using SchoolMs.Modules.Identity.Application;
using SchoolMs.Modules.Identity.Domain;

namespace SchoolMs.Modules.Identity.Endpoints;

public sealed record InviteUserRequest(string FullName, string Email, string? PhoneNumber = null);

public sealed record InvitationResponse(Guid UserId);

/// <summary>From the invitation link: the user id and token, and the password the user chose.</summary>
public sealed record AcceptInvitationRequest(Guid UserId, string Token, string Password);

internal static class InvitationEndpoints
{
    public static void MapInvitationEndpoints(this IEndpointRouteBuilder endpoints)
    {
        // Any signed-in user may invite until the permission model arrives in Phase 1 slice 3,
        // which restricts this to administrators (users.invite).
        endpoints.MapPost("/invitations", InviteAsync)
            .RequireAuthorization()
            .WithName("InviteUser")
            .WithSummary("Invites a user by email (IAM-02). Re-inviting a pending user sends a fresh link.")
            .Produces<InvitationResponse>(StatusCodes.Status202Accepted)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status409Conflict);

        endpoints.MapPost("/invitations/accept", AcceptAsync)
            .AllowAnonymous()
            .RequireRateLimiting(SessionEndpoints.SignInRateLimit)
            .WithName("AcceptInvitation")
            .WithSummary("Sets the first password from an invitation link and signs the user in.")
            .Produces<SessionResponse>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status429TooManyRequests);
    }

    private static async Task<IResult> InviteAsync(InviteUserRequest request, AccountService accounts, CancellationToken cancellationToken)
    {
        var result = await accounts.InviteAsync(request.FullName, request.Email, request.PhoneNumber, cancellationToken);
        return result.IsSuccess
            ? TypedResults.Accepted((string?)null, new InvitationResponse(result.Value))
            : result.Error.ToProblem();
    }

    private static async Task<IResult> AcceptAsync(
        AcceptInvitationRequest request,
        AccountService accounts,
        SignInManager<User> signInManager,
        HttpContext context)
    {
        var result = await accounts.AcceptInvitationAsync(request.UserId, request.Token, request.Password);
        if (result.IsFailure)
        {
            return result.Error.ToProblem();
        }

        await SessionEndpoints.StartSessionAsync(context, signInManager, result.Value);
        return TypedResults.Ok(SessionEndpoints.Session(result.Value));
    }
}
