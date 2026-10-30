using SchoolMs.SharedKernel;

namespace SchoolMs.Modules.Identity.Domain;

internal static class IdentityErrors
{
    public static readonly Error InvalidName =
        Error.Validation("identity.invalid_name", "Name must be between 2 and 200 characters.");

    public static readonly Error InvalidEmail =
        Error.Validation("identity.invalid_email", "Enter a valid email address.");

    public static readonly Error InvalidPhoneNumber =
        Error.Validation("identity.invalid_phone_number", "Enter a valid 10-digit Indian mobile number.");

    public static readonly Error EmailTaken =
        Error.Conflict("identity.email_taken", "A user with this email address already exists.");

    public static readonly Error PhoneNumberTaken =
        Error.Conflict("identity.phone_number_taken", "A user with this mobile number already exists.");

    /// <summary>Deliberately identical for unknown accounts, wrong passwords and inactive accounts (no account enumeration).</summary>
    public static readonly Error InvalidCredentials =
        Error.Unauthorized("identity.invalid_credentials", "Incorrect email, mobile number or password.");

    public static readonly Error LockedOut =
        Error.Forbidden("identity.locked_out", "Too many failed attempts. Try again in 15 minutes, or reset your password.");

    /// <summary>Invitation or reset link that is malformed, expired, already used or superseded by a newer one.</summary>
    public static readonly Error InvalidToken =
        Error.Validation("identity.invalid_token", "This link is invalid or has expired. Ask for a new one.");

    public static Error PasswordRejected(IEnumerable<string> reasons) =>
        Error.Validation(
            "identity.password_rejected",
            "Choose a different password.",
            new Dictionary<string, string[]> { ["password"] = [.. reasons] });
}
