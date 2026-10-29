using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Npgsql;
using SchoolMs.Infrastructure.Health;

namespace SchoolMs.Infrastructure.Persistence;

public sealed class DatabaseOptions
{
    public string ConnectionString { get; set; } = string.Empty;
}

public static class PersistenceExtensions
{
    public const string ConnectionStringName = "Database";

    /// <summary>
    /// Registers the shared <see cref="NpgsqlDataSource"/> (connection pool) and a readiness check.
    /// The connection string comes from <c>ConnectionStrings__Database</c>; a missing value fails at
    /// application start (not at registration, so build-time OpenAPI generation works without a database).
    /// </summary>
    public static IServiceCollection AddPersistence(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString(ConnectionStringName) ?? string.Empty;

        services.AddOptions<DatabaseOptions>()
            .Configure(options => options.ConnectionString = connectionString)
            .Validate(
                options => !string.IsNullOrWhiteSpace(options.ConnectionString),
                $"Connection string '{ConnectionStringName}' is not configured. Set ConnectionStrings__{ConnectionStringName}.")
            .ValidateOnStart();

        // Kerberos/GSS is never used; disabling it stops Npgsql probing for libgssapi on minimal images.
        services.AddNpgsqlDataSource(
            connectionString,
            builder => builder.ConnectionStringBuilder.GssEncryptionMode = GssEncryptionMode.Disable);

        services.AddHealthChecks()
            .AddCheck<DatabaseHealthCheck>("database", HealthStatus.Unhealthy, tags: [HealthTags.Ready]);

        return services;
    }
}
