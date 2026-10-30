using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Mvc.Testing.Handlers;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Npgsql;
using SchoolMs.Infrastructure.Email;
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

    /// <summary>Every email the application sent, instead of delivering it over SMTP.</summary>
    public CapturingEmailSender Emails { get; } = new();

    public async ValueTask InitializeAsync()
    {
        await _postgres.StartAsync();

        // Migrate from a host without the background job server, as the `migrate` command does: the job
        // server must not start before its tables exist.
        await using var migrator = WithWebHostBuilder(builder => builder.UseSetting("Jobs:RunServer", "false"));
        await migrator.Services.MigrateModulesAsync();
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

    /// <summary>
    /// A browser-like client for an organisation's subdomain: keeps cookies and, like the web app, echoes the
    /// antiforgery token in the X-XSRF-TOKEN header on state-changing requests. Call <see cref="SessionClient.StartAsync"/> first.
    /// </summary>
    public SessionClient CreateSessionClient(string slug)
    {
        var baseAddress = new Uri($"http://{slug}.localhost");
        var cookies = new CookieContainer();
        var http = CreateDefaultClient(baseAddress, new AntiforgeryHeaderHandler(cookies), new CookieContainerHandler(cookies));
        return new SessionClient(http, cookies, baseAddress);
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:Database", ConnectionString("schoolms_app", "app_dev"));
        builder.UseSetting("ConnectionStrings:Migrations", ConnectionString("schoolms_migrator", "migrator_dev"));
        builder.UseSetting("App:PublicOrigin", "http://localhost");
        builder.UseSetting("Smtp:Host", "localhost");
        builder.UseSetting("Smtp:FromAddress", "no-reply@schoolms.test");
        builder.UseSetting("Security:RequireSecureCookies", "false");
        builder.UseSetting("Identity:CheckBreachedPasswords", "false");

        builder.ConfigureTestServices(services => services.Replace(ServiceDescriptor.Singleton<IEmailSender>(Emails)));
    }

    private string ConnectionString(string username, string password) =>
        new NpgsqlConnectionStringBuilder(_postgres.GetConnectionString()) { Username = username, Password = password }.ConnectionString;
}

[CollectionDefinition(nameof(ApiCollection))]
public sealed class ApiCollection : ICollectionFixture<ApiFactory>;
