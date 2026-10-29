using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using SchoolMs.Infrastructure.Persistence;
using SchoolMs.IntegrationTests.Infrastructure;
using SchoolMs.Modules.Tenancy.Domain;
using SchoolMs.Modules.Tenancy.Infrastructure;

namespace SchoolMs.IntegrationTests.Tenancy;

/// <summary>
/// Proves both isolation layers of ADR-0002 independently: the EF Core query filter / save guard in the app,
/// and PostgreSQL row-level security in the database. NFR-SEC-04.
/// </summary>
[Collection(nameof(ApiCollection))]
public class TenantIsolationTests(ApiFactory factory)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Queries_ReturnOnlyTheCurrentTenantsRows()
    {
        var a = await factory.CreateOrganisationAsync();
        var b = await factory.CreateOrganisationAsync();

        await using var scope = factory.CreateTenantScope(a.Id, a.Slug);
        var ids = await scope.ServiceProvider.GetRequiredService<TenancyDbContext>().Branches.Select(x => x.Id).ToListAsync(Ct);

        ids.ShouldBe(a.BranchIds, ignoreOrder: true);
        ids.ShouldNotContain(id => b.BranchIds.Contains(id));
    }

    [Fact]
    public async Task DatabaseRls_HidesOtherTenantsRows_EvenWithoutTheAppFilter()
    {
        var a = await factory.CreateOrganisationAsync();
        var b = await factory.CreateOrganisationAsync();

        await using var scope = factory.CreateTenantScope(a.Id, a.Slug);
        var ids = await scope.ServiceProvider.GetRequiredService<TenancyDbContext>().Branches
            .IgnoreQueryFilters([ModuleDbContext.TenantFilter])
            .Select(x => x.Id)
            .ToListAsync(Ct);

        ids.ShouldBe(a.BranchIds, ignoreOrder: true);
        ids.ShouldNotContain(id => b.BranchIds.Contains(id));
    }

    [Fact]
    public async Task WithoutATenant_NoTenantRowsAreVisible()
    {
        await factory.CreateOrganisationAsync();

        await using var scope = factory.Services.CreateAsyncScope();
        var count = await scope.ServiceProvider.GetRequiredService<TenancyDbContext>().Branches
            .IgnoreQueryFilters([ModuleDbContext.TenantFilter])
            .CountAsync(Ct);

        count.ShouldBe(0);
    }

    [Fact]
    public async Task SavingAnotherTenantsRow_IsRejectedByTheApp()
    {
        var a = await factory.CreateOrganisationAsync();
        var b = await factory.CreateOrganisationAsync();

        await using var scope = factory.CreateTenantScope(a.Id, a.Slug);
        var db = scope.ServiceProvider.GetRequiredService<TenancyDbContext>();
        db.Branches.Add(Branch.Create(b.Id, "Intruder", "EVIL", TimeProvider.System).Value);

        await Should.ThrowAsync<TenantIsolationException>(() => db.SaveChangesAsync(Ct));
    }

    [Fact]
    public async Task WritingAnotherTenantsRow_IsRejectedByTheDatabase()
    {
        var a = await factory.CreateOrganisationAsync();
        var b = await factory.CreateOrganisationAsync();

        await using var scope = factory.CreateTenantScope(a.Id, a.Slug);
        var db = scope.ServiceProvider.GetRequiredService<TenancyDbContext>();

        // Raw SQL bypasses the EF save guard, so only RLS stands in the way.
        var error = await Should.ThrowAsync<PostgresException>(() => db.Database.ExecuteSqlAsync(
            $"INSERT INTO tenancy.branches (id, organisation_id, name, code, is_active, created_at) VALUES ({Guid.NewGuid()}, {b.Id}, 'Intruder', 'EVIL', true, now())",
            Ct));

        error.SqlState.ShouldBe(PostgresErrorCodes.InsufficientPrivilege);
    }

    [Fact]
    public async Task EveryTableWithAnOrganisationColumn_HasForcedRowLevelSecurity()
    {
        const string sql = """
            SELECT c.relnamespace::regnamespace::text || '.' || c.relname AS name,
                   c.relrowsecurity AND c.relforcerowsecurity AS protected
            FROM pg_class c
            JOIN pg_attribute a ON a.attrelid = c.oid AND a.attname = 'organisation_id' AND NOT a.attisdropped
            WHERE c.relkind = 'r'
            """;

        var tables = new List<(string Name, bool Protected)>();
        await using (var command = factory.AppDataSource.CreateCommand(sql))
        await using (var reader = await command.ExecuteReaderAsync(Ct))
        {
            while (await reader.ReadAsync(Ct))
            {
                tables.Add((reader.GetString(0), reader.GetBoolean(1)));
            }
        }

        tables.Select(t => t.Name).ShouldContain("tenancy.branches");
        tables.Where(t => !t.Protected).Select(t => t.Name).ShouldBeEmpty("These tables hold tenant data but have no forced RLS policy.");
    }

    [Fact]
    public async Task ApplicationRole_CannotBypassRowLevelSecurity()
    {
        await using var command = factory.AppDataSource.CreateCommand(
            "SELECT rolsuper OR rolbypassrls FROM pg_roles WHERE rolname = current_user");

        var canBypass = (bool)(await command.ExecuteScalarAsync(Ct))!;

        canBypass.ShouldBeFalse();
    }
}
