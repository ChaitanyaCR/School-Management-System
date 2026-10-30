using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SchoolMs.Modules.Identity.Domain;

namespace SchoolMs.Modules.Identity.Infrastructure.Passwords;

/// <summary>
/// NIST SP 800-63B rules not covered by Identity's built-in length check (IAM-04): an upper bound, and no
/// passwords built from the user's own email address. Composition rules ("one symbol, one digit") are
/// deliberately absent; NIST advises against them.
/// </summary>
internal sealed class PasswordPolicyValidator : IPasswordValidator<User>
{
    public const int MinLength = 10;

    /// <summary>Long enough for passphrases; bounded so hashing cost stays predictable.</summary>
    public const int MaxLength = 128;

    public Task<IdentityResult> ValidateAsync(UserManager<User> manager, User user, string? password)
    {
        password ??= string.Empty;
        var errors = new List<IdentityError>();

        if (password.Length > MaxLength)
        {
            errors.Add(new IdentityError
            {
                Code = "PasswordTooLong",
                Description = string.Create(CultureInfo.InvariantCulture, $"Passwords must be at most {MaxLength} characters."),
            });
        }

        var localPart = user.Email.Split('@')[0];
        if (localPart.Length >= 4 && password.Contains(localPart, StringComparison.OrdinalIgnoreCase))
        {
            errors.Add(new IdentityError { Code = "PasswordContainsEmail", Description = "Passwords must not contain your email address." });
        }

        return Task.FromResult(errors.Count == 0 ? IdentityResult.Success : IdentityResult.Failed([.. errors]));
    }
}

/// <summary>
/// Rejects passwords known from data breaches (IAM-04) using the Pwned Passwords range API with k-anonymity:
/// only the first 5 characters of the password's SHA-1 hash leave the server, never the password or the full
/// hash. If the service cannot be reached the check is skipped (fails open) so sign-up never depends on it.
/// </summary>
internal sealed partial class BreachedPasswordValidator(
    PwnedPasswordsClient client,
    IOptions<IdentityModuleOptions> options,
    ILogger<BreachedPasswordValidator> logger) : IPasswordValidator<User>
{
    public async Task<IdentityResult> ValidateAsync(UserManager<User> manager, User user, string? password)
    {
        if (!options.Value.CheckBreachedPasswords || string.IsNullOrEmpty(password))
        {
            return IdentityResult.Success;
        }

        try
        {
            return await client.IsBreachedAsync(password, CancellationToken.None)
                ? IdentityResult.Failed(new IdentityError
                {
                    Code = "PasswordBreached",
                    Description = "This password has appeared in a data breach, so attackers try it first. Choose a different one.",
                })
                : IdentityResult.Success;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            LogUnavailable(logger, ex);
            return IdentityResult.Success;
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Breached-password check skipped: Pwned Passwords is unreachable.")]
    private static partial void LogUnavailable(ILogger logger, Exception exception);
}

/// <summary>Client for https://haveibeenpwned.com/API/v3#PwnedPasswords (free, no API key).</summary>
internal sealed class PwnedPasswordsClient(HttpClient http)
{
    public static readonly Uri BaseAddress = new("https://api.pwnedpasswords.com/");

    public async Task<bool> IsBreachedAsync(string password, CancellationToken cancellationToken)
    {
#pragma warning disable CA5350 // SHA-1 is required by the API's protocol; it is not used to protect anything here.
        var hash = Convert.ToHexString(SHA1.HashData(Encoding.UTF8.GetBytes(password)));
#pragma warning restore CA5350
        var (prefix, suffix) = (hash[..5], hash[5..]);

        using var request = new HttpRequestMessage(HttpMethod.Get, new Uri($"range/{prefix}", UriKind.Relative));
        // Padding hides the real number of matches from anyone observing response sizes.
        request.Headers.Add("Add-Padding", "true");
        using var response = await http.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        // Each line is "SUFFIX:COUNT"; padding entries have a count of 0.
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        foreach (var line in body.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var separator = line.IndexOf(':', StringComparison.Ordinal);
            if (separator == suffix.Length
                && line.AsSpan(0, separator).Equals(suffix, StringComparison.OrdinalIgnoreCase)
                && int.TryParse(line.AsSpan(separator + 1), NumberStyles.None, CultureInfo.InvariantCulture, out var count))
            {
                return count > 0;
            }
        }

        return false;
    }
}
