using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using SchoolMs.Infrastructure.Http;

namespace SchoolMs.Infrastructure.Security;

/// <summary>
/// CSRF protection for the cookie-authenticated JSON API (ADR-0003). The SPA reads the request token from the
/// <see cref="RequestTokenCookie"/> cookie and echoes it in the <see cref="HeaderName"/> header on every
/// state-changing request; the token is bound to the signed-in user, so it is re-issued on sign-in and sign-out.
/// </summary>
public static class ApiAntiforgery
{
    public const string HeaderName = "X-XSRF-TOKEN";

    /// <summary>Readable by JavaScript on purpose: same-origin scripts must copy it into the header.</summary>
    public const string RequestTokenCookie = "XSRF-TOKEN";

    public static IServiceCollection AddApiAntiforgery(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<SecurityOptions>().Bind(configuration.GetSection(SecurityOptions.SectionName));
        services.AddAntiforgery();
        services.AddOptions<AntiforgeryOptions>().Configure<IOptions<SecurityOptions>>((options, security) =>
        {
            options.HeaderName = HeaderName;
            options.Cookie.Name = security.Value.CookieName("xsrf");
            options.Cookie.SecurePolicy = security.Value.CookieSecurePolicy;
            options.Cookie.SameSite = SameSiteMode.Strict;
        });

        return services;
    }

    /// <summary>Rejects state-changing <c>/api</c> requests without a valid token, unless the endpoint opts out.</summary>
    public static IApplicationBuilder UseApiAntiforgery(this IApplicationBuilder app) =>
        app.UseMiddleware<ApiAntiforgeryMiddleware>();

    /// <summary>
    /// Issues a fresh token pair for <see cref="HttpContext.User"/>. After signing in or out within a request,
    /// set <see cref="HttpContext.User"/> to the new principal first, so the token matches the next request.
    /// </summary>
    public static void IssueToken(HttpContext context)
    {
        var antiforgery = context.RequestServices.GetRequiredService<IAntiforgery>();
        var security = context.RequestServices.GetRequiredService<IOptions<SecurityOptions>>().Value;
        var tokens = antiforgery.GetAndStoreTokens(context);

        context.Response.Cookies.Append(RequestTokenCookie, tokens.RequestToken!, new CookieOptions
        {
            HttpOnly = false,
            Secure = security.RequireSecureCookies || context.Request.IsHttps,
            SameSite = SameSiteMode.Strict,
            Path = "/",
        });
    }
}

internal sealed class ApiAntiforgeryMiddleware(RequestDelegate next, IAntiforgery antiforgery)
{
    public async Task InvokeAsync(HttpContext context)
    {
        if (RequiresValidation(context) && !await antiforgery.IsRequestValidAsync(context))
        {
            await ApiProblems.Create(
                    StatusCodes.Status400BadRequest,
                    "antiforgery.invalid",
                    "The request could not be verified. Reload the page and try again.")
                .ExecuteAsync(context);
            return;
        }

        await next(context);
    }

    private static bool RequiresValidation(HttpContext context)
    {
        var method = context.Request.Method;
        if (HttpMethods.IsGet(method) || HttpMethods.IsHead(method) || HttpMethods.IsOptions(method) || HttpMethods.IsTrace(method))
        {
            return false;
        }

        // Endpoints that cannot carry the token (e.g. future webhooks) opt out with .DisableAntiforgery().
        var metadata = context.GetEndpoint()?.Metadata.GetMetadata<IAntiforgeryMetadata>();
        return context.Request.Path.StartsWithSegments("/api", StringComparison.Ordinal) && metadata is not { RequiresValidation: false };
    }
}
