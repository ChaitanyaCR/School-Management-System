using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace SchoolMs.Infrastructure.Configuration;

/// <summary>Product-level settings (NFR-OPS-07): branding and domain are configuration, never hard-coded.</summary>
public sealed class AppOptions
{
    public const string SectionName = "App";

    /// <summary>Display name of the product shown in the UI and emails.</summary>
    [Required]
    public string ProductName { get; init; } = string.Empty;

    /// <summary>Base domain; tenants are served from <c>{org}.{BaseDomain}</c>.</summary>
    [Required]
    public string BaseDomain { get; init; } = string.Empty;

    /// <summary>
    /// Scheme, base domain and port the browser uses, e.g. <c>https://example.in</c> or <c>http://localhost:5173</c>.
    /// Links in emails point to <c>{scheme}://{org}.{host}:{port}</c>.
    /// </summary>
    [Required]
    [Url]
    public string PublicOrigin { get; init; } = string.Empty;

    /// <summary>Serve the OpenAPI document and the API reference UI at <c>/api/docs</c>.</summary>
    public bool ExposeApiDocs { get; init; }
}

/// <summary>Browser-facing links into an organisation's site, e.g. for emails.</summary>
public sealed class TenantLinks(Microsoft.Extensions.Options.IOptions<AppOptions> options)
{
    /// <summary>
    /// The absolute URL of <paramref name="pathAndFragment"/> on the organisation's subdomain.
    /// Secrets belong in the fragment (after <c>#</c>): browsers never send it to a server or in a Referer header.
    /// </summary>
    public Uri For(string slug, string pathAndFragment)
    {
        var origin = new Uri(options.Value.PublicOrigin);
        var builder = new UriBuilder(origin) { Host = $"{slug}.{origin.Host}" };
        return new Uri(builder.Uri, pathAndFragment);
    }
}

public static class AppOptionsExtensions
{
    public static IServiceCollection AddAppOptions(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<AppOptions>()
            .Bind(configuration.GetSection(AppOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();
        services.AddSingleton<TenantLinks>();

        return services;
    }
}
