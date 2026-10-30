using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using SchoolMs.Infrastructure.Http;
using SchoolMs.Modules.Identity.Application;

namespace SchoolMs.Modules.Identity.Endpoints;

public sealed record ForgotPasswordRequest(string Email);

/// <summary>From the reset link: the user id and token, and the new password.</summary>
public sealed record ResetPasswordRequest(Guid UserId, string Token, string Password);

internal static class PasswordEndpoints
{
    public const string EmailRateLimit = "identity-email";

    public static void MapPasswordEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/password/forgot", ForgotAsync)
            .AllowAnonymous()
            .RequireRateLimiting(EmailRateLimit)
            .WithName("ForgotPassword")
            .WithSummary("Emails a password reset link (IAM-03). Always 202, whether or not the account exists.")
            .Produces(StatusCodes.Status202Accepted)
            .ProducesProblem(StatusCodes.Status429TooManyRequests);

        endpoints.MapPost("/password/reset", ResetAsync)
            .AllowAnonymous()
            .RequireRateLimiting(SessionEndpoints.SignInRateLimit)
            .WithName("ResetPassword")
            .WithSummary("Sets a new password from a reset link. Other sessions end and any lockout is cleared.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status429TooManyRequests);
    }

    private static async Task<IResult> ForgotAsync(ForgotPasswordRequest request, AccountService accounts)
    {
        await accounts.RequestPasswordResetAsync(request.Email);
        return TypedResults.Accepted((string?)null);
    }

    private static async Task<IResult> ResetAsync(ResetPasswordRequest request, AccountService accounts) =>
        (await accounts.ResetPasswordAsync(request.UserId, request.Token, request.Password)).ToHttpResult();
}
