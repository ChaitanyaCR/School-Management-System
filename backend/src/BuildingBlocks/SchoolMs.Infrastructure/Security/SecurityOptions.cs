using Microsoft.AspNetCore.Http;

namespace SchoolMs.Infrastructure.Security;

/// <summary>Browser security settings shared by the session and antiforgery cookies.</summary>
public sealed class SecurityOptions
{
    public const string SectionName = "Security";

    /// <summary>
    /// Marks cookies <c>Secure</c> and gives them the <c>__Host-</c> prefix (no Domain, Path=/, HTTPS only).
    /// Always on in real environments; turned off only for plain-HTTP local development and tests.
    /// </summary>
    public bool RequireSecureCookies { get; init; } = true;

    public CookieSecurePolicy CookieSecurePolicy =>
        RequireSecureCookies ? CookieSecurePolicy.Always : CookieSecurePolicy.SameAsRequest;

    /// <summary>The cookie name, with the <c>__Host-</c> prefix when cookies are secure.</summary>
    public string CookieName(string name) => RequireSecureCookies ? $"__Host-{name}" : name;
}
