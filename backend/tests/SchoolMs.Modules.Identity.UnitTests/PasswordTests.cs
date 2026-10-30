using System.Net;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using SchoolMs.Modules.Identity.Domain;
using SchoolMs.Modules.Identity.Infrastructure.Passwords;

namespace SchoolMs.Modules.Identity.UnitTests;

public class Argon2idPasswordHasherTests
{
    private const string Password = "correct horse battery staple";

    private static readonly User User =
        Domain.User.Invite(Guid.CreateVersion7(), "Meena Iyer", "meena@school.test", null, TimeProvider.System).Value;

    private readonly Argon2idPasswordHasher _hasher = new();

    [Fact]
    public void Hash_UsesPhcFormatWithOwaspParameters()
    {
        var hash = _hasher.HashPassword(User, Password);

        hash.ShouldStartWith("$argon2id$v=19$m=19456,t=2,p=1$");
        _hasher.HashPassword(User, Password).ShouldNotBe(hash, "every hash gets a fresh random salt");
    }

    [Fact]
    public void Verify_AcceptsTheRightPasswordOnly()
    {
        var hash = _hasher.HashPassword(User, Password);

        _hasher.VerifyHashedPassword(User, hash, Password).ShouldBe(PasswordVerificationResult.Success);
        _hasher.VerifyHashedPassword(User, hash, Password + " ").ShouldBe(PasswordVerificationResult.Failed);
    }

    [Fact]
    public void Verify_AcceptsHashesFromTheReferenceImplementation()
    {
        // Produced by argon2-cffi (the reference C implementation): proves the format and parameter units match.
        const string reference = "$argon2id$v=19$m=19456,t=2,p=1$c2Nob29sbXMtc2FsdC0xNg$yKotdDsGq9fNliPanrp/dxPLxjjGL3uOO+BJ3ybTzRc";

        _hasher.VerifyHashedPassword(User, reference, Password).ShouldBe(PasswordVerificationResult.Success);
        _hasher.VerifyHashedPassword(User, reference, "wrong").ShouldBe(PasswordVerificationResult.Failed);
    }

    [Fact]
    public void Verify_AsksForRehashWhenParametersAreWeaker()
    {
        const string weaker = "$argon2id$v=19$m=8192,t=1,p=1$c2Nob29sbXMtc2FsdC0xNg$rAmAoiIJtzcid8EjisHjxm/zK/GWwj9gu2zFvdZ2jfA";

        _hasher.VerifyHashedPassword(User, weaker, Password).ShouldBe(PasswordVerificationResult.SuccessRehashNeeded);
    }

    [Theory]
    [InlineData("")]
    [InlineData("AQAAAAIAAYagAAAAEJ")] // ASP.NET Core Identity PBKDF2 format
    [InlineData("$argon2i$v=19$m=19456,t=2,p=1$c2Nob29sbXMtc2FsdC0xNg$yKotdDsGq9fNliPanrp/dxPLxjjGL3uOO+BJ3ybTzRc")]
    [InlineData("$argon2id$v=19$m=19456,t=2,p=4$c2Nob29sbXMtc2FsdC0xNg$yKotdDsGq9fNliPanrp/dxPLxjjGL3uOO+BJ3ybTzRc")]
    [InlineData("$argon2id$v=19$m=19456,t=2,p=1$not base64!$yKotdDsGq9fNliPanrp")]
    public void Verify_RejectsMalformedHashes(string hash)
    {
        _hasher.VerifyHashedPassword(User, hash, Password).ShouldBe(PasswordVerificationResult.Failed);
    }
}

public class PasswordPolicyValidatorTests
{
    private static readonly User User =
        Domain.User.Invite(Guid.CreateVersion7(), "Meena Iyer", "meena.iyer@school.test", null, TimeProvider.System).Value;

    private readonly PasswordPolicyValidator _validator = new();

    [Fact]
    public async Task AcceptsAPassphrase()
    {
        (await _validator.ValidateAsync(null!, User, "blue river kettle under the bridge")).Succeeded.ShouldBeTrue();
    }

    [Fact]
    public async Task RejectsOverlongPasswords()
    {
        var result = await _validator.ValidateAsync(null!, User, new string('a', PasswordPolicyValidator.MaxLength + 1));

        result.Errors.ShouldContain(e => e.Code == "PasswordTooLong");
    }

    [Fact]
    public async Task RejectsPasswordsContainingTheEmailAddress()
    {
        var result = await _validator.ValidateAsync(null!, User, "MEENA.IYER-2026!");

        result.Errors.ShouldContain(e => e.Code == "PasswordContainsEmail");
    }
}

public class BreachedPasswordValidatorTests
{
    private const string Password = "password1234";

    private static readonly User User =
        Domain.User.Invite(Guid.CreateVersion7(), "Meena Iyer", "meena@school.test", null, TimeProvider.System).Value;

    private static string Suffix => Sha1(Password)[5..];

    [Fact]
    public async Task RejectsBreachedPasswords_SendingOnlyTheHashPrefix()
    {
        var handler = new StubHandler($"0000000000000000000000000000000000A:0\r\n{Suffix}:2817\r\n");

        var result = await Validator(handler).ValidateAsync(null!, User, Password);

        result.Errors.ShouldContain(e => e.Code == "PasswordBreached");
        handler.RequestedPath.ShouldBe("/range/" + Sha1(Password)[..5]);
        handler.Padded.ShouldBeTrue();
    }

    [Fact]
    public async Task IgnoresPaddingEntries()
    {
        var result = await Validator(new StubHandler($"{Suffix}:0\r\n")).ValidateAsync(null!, User, Password);

        result.Succeeded.ShouldBeTrue();
    }

    [Fact]
    public async Task AcceptsUnknownPasswords()
    {
        var result = await Validator(new StubHandler("0000000000000000000000000000000000A:3\r\n")).ValidateAsync(null!, User, Password);

        result.Succeeded.ShouldBeTrue();
    }

    [Fact]
    public async Task FailsOpenWhenTheServiceIsUnavailable()
    {
        var result = await Validator(new StubHandler(status: HttpStatusCode.ServiceUnavailable)).ValidateAsync(null!, User, Password);

        result.Succeeded.ShouldBeTrue();
    }

    [Fact]
    public async Task CanBeTurnedOff()
    {
        var handler = new StubHandler($"{Suffix}:2817\r\n");

        var result = await Validator(handler, enabled: false).ValidateAsync(null!, User, Password);

        result.Succeeded.ShouldBeTrue();
        handler.RequestedPath.ShouldBeNull();
    }

    private static BreachedPasswordValidator Validator(StubHandler handler, bool enabled = true) =>
        new(
            new PwnedPasswordsClient(new HttpClient(handler) { BaseAddress = PwnedPasswordsClient.BaseAddress }),
            Options.Create(new IdentityModuleOptions { CheckBreachedPasswords = enabled }),
            NullLogger<BreachedPasswordValidator>.Instance);

#pragma warning disable CA5350 // Mirrors the API's protocol.
    private static string Sha1(string value) => Convert.ToHexString(SHA1.HashData(Encoding.UTF8.GetBytes(value)));
#pragma warning restore CA5350

    private sealed class StubHandler(string body = "", HttpStatusCode status = HttpStatusCode.OK) : HttpMessageHandler
    {
        public string? RequestedPath { get; private set; }

        public bool Padded { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestedPath = request.RequestUri!.AbsolutePath;
            Padded = request.Headers.TryGetValues("Add-Padding", out var values) && values.Contains("true");
            return Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent(body) });
        }
    }
}
