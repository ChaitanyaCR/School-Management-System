using System.ComponentModel.DataAnnotations;

namespace SchoolMs.Api.Configuration;

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

    /// <summary>Serve the OpenAPI document and the API reference UI at <c>/api/docs</c>.</summary>
    public bool ExposeApiDocs { get; init; }
}

internal static class AppOptionsExtensions
{
    public static IServiceCollection AddAppOptions(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<AppOptions>()
            .Bind(configuration.GetSection(AppOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        return services;
    }
}
