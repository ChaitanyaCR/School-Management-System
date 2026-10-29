using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using SchoolMs.Infrastructure.Persistence;
using SchoolMs.Infrastructure.Tenancy;
using Testcontainers.PostgreSql;

namespace SchoolMs.IntegrationTests.Infrastructure;

/// <summary>
/// Boots the API against a throwaway PostgreSQL container set up exactly like real environments:
/// the same roles (infra/postgres/init), migrations applied by the migrator, and the app connecting as the
/// RLS-restricted application role. One container is shared by every test class in <see cref="ApiCollection"/>;
/// tests create their own uniquely named data and must not depend on each other.
/// </summary>
public sealed class ApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    public const string PostgresImage = "postgres:17-alpine";

    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder(PostgresImage)
        .WithDatabase("schoolms")
        .WithResourceMapping(new DirectoryInfo(Path.Combine(AppContext.BaseDirectory, "postgres-init")), "/docker-entrypoint-initdb.d/")
        .Build();

    public async ValueTask InitializeAsync()
    {
        await _postgres.StartAsync();
        await Services.MigrateModulesAsync();
    }

    public override async ValueTask DisposeAsync()
    {
        await _postgres.DisposeAsync();
        await base.DisposeAsync();
    }

    /// <summary>Connection as the application role (RLS applies), for raw SQL assertions.</summary>
    public NpgsqlDataSource AppDataSource => Services.GetRequiredService<NpgsqlDataSource>();

    /// <summary>An HTTP client addressed to an organisation's subdomain, e.g. http://greenwood.localhost.</summary>
    public HttpClient CreateTenantClient(string slug) =>
        CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri($"http://{slug}.localhost") });

    /// <summary>A DI scope acting on behalf of one organisation, as a request to its subdomain would.</summary>
    public AsyncServiceScope CreateTenantScope(Guid organisationId, string slug)
    {
        var scope = Services.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<TenantContext>().SetTenant(organisationId, slug);
        return scope;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:Database", ConnectionString("schoolms_app", "app_dev"));
        builder.UseSetting("ConnectionStrings:Migrations", ConnectionString("schoolms_migrator", "migrator_dev"));
    }

    private string ConnectionString(string username, string password) =>
        new NpgsqlConnectionStringBuilder(_postgres.GetConnectionString()) { Username = username, Password = password }.ConnectionString;
}

[CollectionDefinition(nameof(ApiCollection))]
public sealed class ApiCollection : ICollectionFixture<ApiFactory>;
