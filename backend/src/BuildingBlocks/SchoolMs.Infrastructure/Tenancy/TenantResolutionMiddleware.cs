using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;
using Serilog.Context;

namespace SchoolMs.Infrastructure.Tenancy;

/// <summary>
/// Resolves the tenant from the host name: <c>{slug}.{BaseDomain}</c> selects an organisation, the bare base
/// domain is the platform (no tenant). Unknown hosts and suspended organisations are rejected before any
/// endpoint runs (PLT-02, PLT-04).
/// </summary>
public sealed class TenantResolutionMiddleware(
    RequestDelegate next,
    TenantResolutionOptions options,
    IMemoryCache cache)
{
    private static readonly TimeSpan CacheDuration = TimeSpan.FromSeconds(60);

    public async Task InvokeAsync(HttpContext context, TenantContext tenantContext, ITenantLookup lookup)
    {
        if (context.Request.Path.StartsWithSegments("/health", StringComparison.Ordinal))
        {
            await next(context);
            return;
        }

        var host = context.Request.Host.Host.ToLowerInvariant();
        if (host == options.BaseDomain)
        {
            await next(context);
            return;
        }

        var suffix = "." + options.BaseDomain;
        var slug = host.EndsWith(suffix, StringComparison.Ordinal) ? host[..^suffix.Length] : null;
        if (string.IsNullOrEmpty(slug) || slug.Contains('.', StringComparison.Ordinal))
        {
            await WriteProblemAsync(context, StatusCodes.Status404NotFound, "tenant.unknown_host", "Unknown address.");
            return;
        }

        var tenant = await cache.GetOrCreateAsync(
            CacheKey(slug),
            entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = CacheDuration;
                return lookup.FindBySlugAsync(slug, context.RequestAborted);
            });

        if (tenant is null)
        {
            await WriteProblemAsync(context, StatusCodes.Status404NotFound, "tenant.not_found", "No school is registered at this address.");
            return;
        }

        if (!tenant.IsActive)
        {
            await WriteProblemAsync(context, StatusCodes.Status403Forbidden, "tenant.suspended", "This school's account is suspended.");
            return;
        }

        tenantContext.SetTenant(tenant.OrganisationId, tenant.Slug);
        using (LogContext.PushProperty("Tenant", tenant.Slug))
        {
            await next(context);
        }
    }

    /// <summary>Cache key for a slug; evict it when an organisation's slug or status changes.</summary>
    public static string CacheKey(string slug) => $"tenant:{slug}";

    private static Task WriteProblemAsync(HttpContext context, int status, string code, string title) =>
        Results.Problem(statusCode: status, title: title, extensions: new Dictionary<string, object?> { ["code"] = code })
            .ExecuteAsync(context);
}

public sealed record TenantResolutionOptions(string BaseDomain);
