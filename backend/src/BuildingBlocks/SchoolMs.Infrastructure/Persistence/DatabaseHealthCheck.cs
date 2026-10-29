using Microsoft.Extensions.Diagnostics.HealthChecks;
using Npgsql;

namespace SchoolMs.Infrastructure.Persistence;

internal sealed class DatabaseHealthCheck(NpgsqlDataSource dataSource) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            await using var command = dataSource.CreateCommand("SELECT 1");
            await command.ExecuteScalarAsync(cancellationToken);
            return HealthCheckResult.Healthy();
        }
        catch (Exception ex) when (ex is NpgsqlException or TimeoutException)
        {
            // Exception details stay in the logs; the health response only reports status.
            return HealthCheckResult.Unhealthy("Database is unreachable.", ex);
        }
    }
}
