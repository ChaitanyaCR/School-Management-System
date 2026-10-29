using SchoolMs.SharedKernel;

namespace SchoolMs.Modules.Tenancy.Domain;

internal static class TenancyErrors
{
    public static readonly Error InvalidName =
        Error.Validation("tenancy.invalid_name", "Name must be between 2 and 200 characters.");

    public static readonly Error InvalidSlug =
        Error.Validation("tenancy.invalid_slug", "Address must be 3–30 lowercase letters, digits or hyphens, and cannot start or end with a hyphen.");

    public static readonly Error ReservedSlug =
        Error.Validation("tenancy.reserved_slug", "This address is reserved. Choose another.");

    public static readonly Error InvalidBranchCode =
        Error.Validation("tenancy.invalid_branch_code", "Branch code must be 2–10 uppercase letters or digits.");
}
