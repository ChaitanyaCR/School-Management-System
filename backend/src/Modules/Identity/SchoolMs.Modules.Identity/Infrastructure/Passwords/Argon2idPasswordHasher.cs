using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Identity;
using NSec.Cryptography;
using SchoolMs.Modules.Identity.Domain;

namespace SchoolMs.Modules.Identity.Infrastructure.Passwords;

/// <summary>
/// Argon2id password hashing (NFR-SEC-03) through libsodium, replacing Identity's PBKDF2 default.
/// Parameters follow the OWASP Password Storage Cheat Sheet (m = 19 MiB, t = 2, p = 1). Hashes use the standard
/// PHC string format (<c>$argon2id$v=19$m=…,t=…,p=1$salt$hash</c>), so they stay verifiable by any Argon2 library.
/// Stored hashes with weaker parameters are re-hashed with the current ones at the user's next sign-in.
/// </summary>
internal sealed class Argon2idPasswordHasher : IPasswordHasher<User>
{
    public const int MemoryKiB = 19 * 1024;
    public const int Iterations = 2;
    private const int SaltBytes = 16;
    private const int HashBytes = 32;
    private const string Prefix = "$argon2id$v=19$";

    // A real hash to verify against when there is no user, so "no such user" takes as long as a wrong password.
    private static readonly Lazy<string> DummyHash = new(() => Hash("not-a-real-password", MemoryKiB, Iterations));

    public string HashPassword(User user, string password) => Hash(password, MemoryKiB, Iterations);

    public PasswordVerificationResult VerifyHashedPassword(User user, string hashedPassword, string providedPassword)
    {
        if (!TryParse(hashedPassword, out var memoryKiB, out var iterations, out var salt, out var expected))
        {
            return PasswordVerificationResult.Failed;
        }

        var actual = Derive(providedPassword, salt, memoryKiB, iterations, expected.Length);
        if (!CryptographicOperations.FixedTimeEquals(actual, expected))
        {
            return PasswordVerificationResult.Failed;
        }

        return memoryKiB < MemoryKiB || iterations < Iterations
            ? PasswordVerificationResult.SuccessRehashNeeded
            : PasswordVerificationResult.Success;
    }

    /// <summary>Spends the same time as verifying a real password; used when the account does not exist.</summary>
    public void SimulateVerification(string providedPassword) =>
        VerifyHashedPassword(null!, DummyHash.Value, providedPassword);

    internal static string Hash(string password, int memoryKiB, int iterations)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltBytes);
        var hash = Derive(password, salt, memoryKiB, iterations, HashBytes);
        return string.Create(
            CultureInfo.InvariantCulture,
            $"{Prefix}m={memoryKiB},t={iterations},p=1${Encode(salt)}${Encode(hash)}");
    }

    private static byte[] Derive(string password, byte[] salt, int memoryKiB, int iterations, int length)
    {
        var algorithm = PasswordBasedKeyDerivationAlgorithm.Argon2id(new Argon2Parameters
        {
            DegreeOfParallelism = 1,
            MemorySize = memoryKiB,
            NumberOfPasses = iterations,
        });
        return algorithm.DeriveBytes(Encoding.UTF8.GetBytes(password), salt, length);
    }

    private static bool TryParse(string phc, out int memoryKiB, out int iterations, out byte[] salt, out byte[] hash)
    {
        memoryKiB = iterations = 0;
        salt = hash = [];

        // ["", "argon2id", "v=19", "m=19456,t=2,p=1", salt, hash]
        var parts = phc.Split('$');
        if (parts.Length != 6 || parts[1] != "argon2id" || parts[2] != "v=19")
        {
            return false;
        }

        var parameters = parts[3].Split(',');
        if (parameters.Length != 3
            || !TryReadParameter(parameters[0], "m=", out memoryKiB)
            || !TryReadParameter(parameters[1], "t=", out iterations)
            || parameters[2] != "p=1")
        {
            return false;
        }

        return TryDecode(parts[4], out salt) && salt.Length == SaltBytes && TryDecode(parts[5], out hash) && hash.Length >= 16;
    }

    private static bool TryReadParameter(string text, string name, out int value)
    {
        value = 0;
        return text.StartsWith(name, StringComparison.Ordinal)
            && int.TryParse(text.AsSpan(name.Length), NumberStyles.None, CultureInfo.InvariantCulture, out value)
            && value > 0;
    }

    // PHC strings use standard Base64 without padding.
    private static string Encode(byte[] bytes) => Convert.ToBase64String(bytes).TrimEnd('=');

    private static bool TryDecode(string text, out byte[] bytes)
    {
        var padded = text.PadRight(text.Length + ((4 - (text.Length % 4)) % 4), '=');
        bytes = new byte[padded.Length];
        if (Convert.TryFromBase64String(padded, bytes, out var written))
        {
            bytes = bytes[..written];
            return true;
        }

        bytes = [];
        return false;
    }
}
