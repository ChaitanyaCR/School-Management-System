using SchoolMs.Infrastructure.Persistence;

namespace SchoolMs.Api;

/// <summary>
/// One-off commands run with the API binary instead of serving traffic, e.g. before a deployment:
/// <c>dotnet SchoolMs.Api.dll migrate</c>, or <c>migrate seed-demo</c> for local environments.
/// </summary>
internal static partial class HostCommands
{
    public const string Migrate = "migrate";
    public const string SeedDemo = "seed-demo";

    public static async Task<bool> TryRunAsync(WebApplication app, string[] args)
    {
        var commands = args.Where(a => a is Migrate or SeedDemo).ToHashSet(StringComparer.Ordinal);
        if (commands.Count == 0)
        {
            return false;
        }

        if (commands.Contains(Migrate))
        {
            await app.Services.MigrateModulesAsync();
            LogCompleted(app.Logger, Migrate);
        }

        if (commands.Contains(SeedDemo))
        {
            if (app.Environment.IsProduction())
            {
                throw new InvalidOperationException("Demo data must never be seeded into a production environment.");
            }

            await app.Services.SeedAsync();
            LogCompleted(app.Logger, SeedDemo);
        }

        return true;
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Command '{Command}' completed.")]
    private static partial void LogCompleted(ILogger logger, string command);
}
