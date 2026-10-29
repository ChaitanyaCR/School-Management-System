using System.Globalization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.DependencyInjection;
using SchoolMs.Infrastructure.Http;
using SchoolMs.SharedKernel;

namespace SchoolMs.Infrastructure.Security;

/// <summary>
/// Request throttling for abuse-prone endpoints (IAM-05). Modules add named policies with
/// <c>services.Configure&lt;RateLimiterOptions&gt;(o =&gt; o.AddPolicy(...))</c> and apply them with
/// <c>.RequireRateLimiting(name)</c>.
/// </summary>
public static class RateLimiting
{
    public static IServiceCollection AddApiRateLimiting(this IServiceCollection services) =>
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.OnRejected = async (context, cancellationToken) =>
            {
                if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
                {
                    context.HttpContext.Response.Headers.RetryAfter =
                        ((int)Math.Ceiling(retryAfter.TotalSeconds)).ToString(CultureInfo.InvariantCulture);
                }

                await ApiProblems.Create(
                        StatusCodes.Status429TooManyRequests,
                        "rate_limited",
                        "Too many attempts. Wait a minute and try again.")
                    .ExecuteAsync(context.HttpContext);
            };
        });

    /// <summary>
    /// Partition key per organisation and client IP address. Staff of one school often share a single public
    /// IP address, so limits on this key must be generous; per-account lockout is the main brute-force defence.
    /// Behind a reverse proxy the IP comes from X-Forwarded-For (ASPNETCORE_FORWARDEDHEADERS_ENABLED).
    /// </summary>
    public static string ClientKey(HttpContext context)
    {
        var tenant = context.RequestServices.GetRequiredService<ITenantContext>().Slug ?? "-";
        return $"{tenant}|{context.Connection.RemoteIpAddress}";
    }

    /// <summary>A fixed-window policy keyed by <see cref="ClientKey"/>, rejecting (not queueing) excess requests.</summary>
    public static void AddClientFixedWindowPolicy(this RateLimiterOptions options, string name, int permitLimit, TimeSpan window) =>
        options.AddPolicy(name, context => RateLimitPartition.GetFixedWindowLimiter(
            ClientKey(context),
            _ => new FixedWindowRateLimiterOptions { PermitLimit = permitLimit, Window = window, QueueLimit = 0 }));
}
