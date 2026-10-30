using Hangfire;
using Hangfire.PostgreSql;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using SchoolMs.Infrastructure.Persistence;

namespace SchoolMs.Infrastructure.Jobs;

public sealed class JobsOptions
{
    public const string SectionName = "Jobs";

    /// <summary>Process jobs in this instance. Turn off on instances that should only serve HTTP.</summary>
    public bool RunServer { get; init; } = true;

    public int WorkerCount { get; init; } = 2;
}

/// <summary>
/// Background jobs on Hangfire with PostgreSQL storage, so no extra infrastructure is needed (ADR-0005).
/// Jobs that touch tenant data take a <see cref="Tenancy.TenantRef"/> argument and set the tenant first,
/// exactly as a request does.
/// </summary>
public static class BackgroundJobs
{
    public const string Schema = "hangfire";

    public static IServiceCollection AddBackgroundJobs(this IServiceCollection services, IConfiguration configuration)
    {
        var options = configuration.GetSection(JobsOptions.SectionName).Get<JobsOptions>() ?? new JobsOptions();

        services.AddHangfire((sp, config) => config
            .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
            .UseSimpleAssemblyNameTypeSerializer()
            .UseRecommendedSerializerSettings()
            .UsePostgreSqlStorage(
                bootstrapper => bootstrapper.UseConnectionFactory(new DataSourceConnectionFactory(sp.GetRequiredService<NpgsqlDataSource>())),
                new PostgreSqlStorageOptions
                {
                    SchemaName = Schema,
                    // The application role cannot run DDL; the tables are created by `migrate` (JobStorageInstaller).
                    PrepareSchemaIfNecessary = false,
                    // LISTEN/NOTIFY: workers pick up new jobs immediately instead of polling every few seconds.
                    EnableLongPolling = true,
                }));

        if (options.RunServer)
        {
            services.AddHangfireServer(server => server.WorkerCount = options.WorkerCount);
        }

        services.AddSingleton<IDatabaseInstaller, JobStorageInstaller>();
        return services;
    }

    /// <summary>Hangfire connections come from the shared pool (application role, as for EF Core).</summary>
    private sealed class DataSourceConnectionFactory(NpgsqlDataSource dataSource) : IConnectionFactory
    {
        public NpgsqlConnection GetOrCreateConnection() => dataSource.CreateConnection();
    }

    private sealed class JobStorageInstaller : IDatabaseInstaller
    {
        public async Task InstallAsync(string migrationsConnectionString, CancellationToken cancellationToken)
        {
            await using var connection = new NpgsqlConnection(migrationsConnectionString);
            await connection.OpenAsync(cancellationToken);
            PostgreSqlObjectsInstaller.Install(connection, Schema);
        }
    }
}
