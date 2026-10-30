using SchoolMs.Infrastructure.Modules;
using SchoolMs.Modules.Identity;
using SchoolMs.Modules.Tenancy;

namespace SchoolMs.Api;

/// <summary>Every business module in the application, in registration order.</summary>
internal static class AppModules
{
    public static readonly IReadOnlyList<IModule> All =
    [
        new TenancyModule(),
        new IdentityModule(),
        // Modules are added here as phases deliver them.
    ];
}
