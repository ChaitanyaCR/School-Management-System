using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Routing;
using SchoolMs.Infrastructure.Http;
using SchoolMs.Infrastructure.Security;
using SchoolMs.Modules.Identity.Application;
using SchoolMs.Modules.Identity.Domain;

namespace SchoolMs.Modules.Identity.Endpoints;

/// <summary>The signed-in user, or null when nobody is signed in.</summary>
public sealed record SessionResponse(CurrentUserResponse? User);

public sealed record CurrentUserResponse(Guid Id, string FullName, string Email);

/// <summary>Signs in with an email address or a 10-digit mobile number (IAM-01).</summary>
public sealed record SignInRequest(string Identifier, string Password, bool RememberMe = false);

internal static class SessionEndpoints
{
    public const string SignInRateLimit = "identity-sign-in";

    public static void MapSessionEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/session", GetAsync)
            .AllowAnonymous()
            .WithName("GetSession")
            .WithSummary("The signed-in user. Also issues the antiforgery token the app sends with every change.")
            .Produces<SessionResponse>();

        endpoints.MapPost("/session", SignInAsync)
            .AllowAnonymous()
            .RequireRateLimiting(SignInRateLimit)
            .WithName("SignIn")
            .WithSummary("Signs in and starts a session (cookie).")
            .Produces<SessionResponse>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status429TooManyRequests);

        endpoints.MapDelete("/session", SignOutAsync)
            .AllowAnonymous()
            .WithName("SignOut")
            .WithSummary("Ends the session.")
            .Produces(StatusCodes.Status204NoContent);
    }

    private static async Task<IResult> GetAsync(HttpContext context, UserManager<User> users)
    {
        ApiAntiforgery.IssueToken(context);
        var user = context.User.Identity?.IsAuthenticated == true ? await users.GetUserAsync(context.User) : null;
        return TypedResults.Ok(Session(user is { IsActive: true } ? user : null));
    }

    private static async Task<IResult> SignInAsync(
        SignInRequest request,
        AccountService accounts,
        SignInManager<User> signInManager,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        var result = await accounts.SignInAsync(request.Identifier, request.Password, request.RememberMe, cancellationToken);
        if (result.IsFailure)
        {
            return result.Error.ToProblem();
        }

        await StartSessionAsync(context, signInManager, result.Value);
        return TypedResults.Ok(Session(result.Value));
    }

    private static async Task<IResult> SignOutAsync(HttpContext context, SignInManager<User> signInManager)
    {
        await signInManager.SignOutAsync();
        context.User = new ClaimsPrincipal(new ClaimsIdentity());
        ApiAntiforgery.IssueToken(context);
        return TypedResults.NoContent();
    }

    /// <summary>
    /// After signing in: the antiforgery token is bound to the user, so issue one for the new principal now,
    /// letting the app continue without another round trip.
    /// </summary>
    internal static async Task StartSessionAsync(HttpContext context, SignInManager<User> signInManager, User user)
    {
        context.User = await signInManager.CreateUserPrincipalAsync(user);
        ApiAntiforgery.IssueToken(context);
    }

    internal static SessionResponse Session(User? user) =>
        new(user is null ? null : new CurrentUserResponse(user.Id, user.FullName, user.Email));
}
