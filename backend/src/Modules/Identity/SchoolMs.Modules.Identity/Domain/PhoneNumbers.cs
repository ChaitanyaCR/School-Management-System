using System.Text;
using SchoolMs.SharedKernel;

namespace SchoolMs.Modules.Identity.Domain;

/// <summary>
/// Indian mobile numbers. Accepts the common ways people type them ("98765 43210", "098765-43210",
/// "+91 98765 43210") and stores E.164 ("+919876543210"). Other countries are added with other boards/regions.
/// </summary>
internal static class PhoneNumbers
{
    public static Result<string> Normalise(string input)
    {
        var digits = new StringBuilder(input.Length);
        foreach (var c in input.Trim())
        {
            if (char.IsAsciiDigit(c))
            {
                digits.Append(c);
            }
            else if (c is not (' ' or '-' or '(' or ')' or '+'))
            {
                return IdentityErrors.InvalidPhoneNumber;
            }
        }

        var number = digits.ToString() switch
        {
            { Length: 12 } d when d.StartsWith("91", StringComparison.Ordinal) => d[2..],
            { Length: 11 } d when d.StartsWith('0') => d[1..],
            var d => d,
        };

        // Indian mobile numbers have 10 digits and start with 6, 7, 8 or 9.
        return number.Length == 10 && number[0] is >= '6' and <= '9'
            ? "+91" + number
            : IdentityErrors.InvalidPhoneNumber;
    }
}
