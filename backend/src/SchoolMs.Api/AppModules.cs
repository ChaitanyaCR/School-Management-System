using SchoolMs.Infrastructure.Modules;

namespace SchoolMs.Api;

/// <summary>Every business module in the application, in registration order.</summary>
internal static class AppModules
{
    public static readonly IReadOnlyList<IModule> All =
    [
        // Modules are added here as phases deliver them (Phase 1: Tenancy, Identity, Audit).
    ];
}
