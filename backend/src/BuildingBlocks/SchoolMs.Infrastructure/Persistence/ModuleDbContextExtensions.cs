using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Npgsql.EntityFrameworkCore.PostgreSQL.Infrastructure;

namespace SchoolMs.Infrastructure.Persistence;

/// <summary>A module's DbContext, with a way to build it on the migrator connection.</summary>
public sealed record ModuleDbContextRegistration(Type ContextType, Func<IServiceProvider, string, DbContext> CreateForMigration);

/// <summary>Runs before any app traffic: e.g. demo data. Implemented by modules that need it.</summary>
public interface IDataSeeder
{
    Task SeedAsync(CancellationToken cancellationToken);
}

public static class ModuleDbContextExtensions
{
    public const string MigrationsConnectionStringName = "Migrations";

    /// <summary>
    /// Registers a module DbContext on the shared connection pool (application role, RLS enforced) with
    /// the tenant interceptors, plus a factory for running its migrations as the migrator role.
    /// </summary>
    public static IServiceCollection AddModuleDbContext<TContext>(this IServiceCollection services, string schema)
        where TContext : ModuleDbContext
    {
        services.AddScoped<TenantRlsConnectionInterceptor>();
        services.AddScoped<TenantGuardInterceptor>();

        services.AddDbContext<TContext>((sp, options) => options
            .UseNpgsql(sp.GetRequiredService<NpgsqlDataSource>(), npgsql => ConfigureNpgsql(npgsql, schema))
            .UseSnakeCaseNamingConvention()
            .AddInterceptors(
                sp.GetRequiredService<TenantRlsConnectionInterceptor>(),
                sp.GetRequiredService<TenantGuardInterceptor>()));

        services.AddSingleton(new ModuleDbContextRegistration(typeof(TContext), (sp, connectionString) =>
        {
            var options = new DbContextOptionsBuilder<TContext>()
                .UseNpgsql(connectionString, npgsql => ConfigureNpgsql(npgsql, schema))
                .UseSnakeCaseNamingConvention()
                .Options;
            return ActivatorUtilities.CreateInstance<TContext>(sp, options);
        }));

        return services;
    }

    /// <summary>Applies pending migrations of every module, in registration order, as the migrator role.</summary>
    public static async Task MigrateModulesAsync(this IServiceProvider services, CancellationToken cancellationToken = default)
    {
        var connectionString = services.GetRequiredService<IConfiguration>().GetConnectionString(MigrationsConnectionStringName);
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                $"Connection string '{MigrationsConnectionStringName}' is not configured. Set ConnectionStrings__{MigrationsConnectionStringName}.");
        }

        using var scope = services.CreateScope();
        foreach (var registration in scope.ServiceProvider.GetServices<ModuleDbContextRegistration>())
        {
            await using var context = registration.CreateForMigration(scope.ServiceProvider, connectionString);
            await context.Database.MigrateAsync(cancellationToken);
        }
    }

    /// <summary>Runs every registered <see cref="IDataSeeder"/>, each in its own scope (so each may set its own tenant).</summary>
    public static async Task SeedAsync(this IServiceProvider services, CancellationToken cancellationToken = default)
    {
        int count;
        using (var probe = services.CreateScope())
        {
            count = probe.ServiceProvider.GetServices<IDataSeeder>().Count();
        }

        for (var i = 0; i < count; i++)
        {
            using var scope = services.CreateScope();
            await scope.ServiceProvider.GetServices<IDataSeeder>().ElementAt(i).SeedAsync(cancellationToken);
        }
    }

    private static void ConfigureNpgsql(NpgsqlDbContextOptionsBuilder npgsql, string schema) =>
        npgsql.MigrationsHistoryTable("__ef_migrations_history", schema);
}
