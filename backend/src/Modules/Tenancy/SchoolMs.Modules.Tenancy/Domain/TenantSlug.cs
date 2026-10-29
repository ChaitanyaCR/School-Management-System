using System.Text.RegularExpressions;
using SchoolMs.SharedKernel;

namespace SchoolMs.Modules.Tenancy.Domain;

/// <summary>The subdomain that identifies an organisation, e.g. "greenwood" in greenwood.example.in.</summary>
internal static partial class TenantSlug
{
    /// <summary>Subdomains kept for the platform itself.</summary>
    private static readonly HashSet<string> Reserved =
    [
        "www", "api", "app", "admin", "platform", "mail", "smtp", "static", "assets", "cdn",
        "status", "support", "help", "docs", "blog", "login", "auth", "account", "billing",
    ];

    public static Result<string> Normalise(string? slug)
    {
        var value = slug?.Trim().ToLowerInvariant() ?? string.Empty;

        if (!Pattern().IsMatch(value))
        {
            return TenancyErrors.InvalidSlug;
        }

        return Reserved.Contains(value) ? TenancyErrors.ReservedSlug : value;
    }

    [GeneratedRegex("^[a-z0-9][a-z0-9-]{1,28}[a-z0-9]$", RegexOptions.CultureInvariant)]
    private static partial Regex Pattern();
}
