using Microsoft.Extensions.DependencyInjection;
using SchoolMs.IntegrationTests.Infrastructure;
using SchoolMs.Modules.Tenancy.Domain;
using SchoolMs.Modules.Tenancy.Infrastructure;

namespace SchoolMs.IntegrationTests.Tenancy;

internal sealed record TestTenant(Guid Id, string Slug, Guid[] BranchIds);

/// <summary>Creates organisations through the real DbContext path (tenant set, interceptors active).</summary>
internal static class TenancyTestData
{
    public static string UniqueSlug() => "t" + Guid.NewGuid().ToString("N")[..12];

    public static async Task<TestTenant> CreateOrganisationAsync(this ApiFactory factory, int branches = 2, bool suspended = false)
    {
        var clock = TimeProvider.System;
        var organisation = Organisation.Create("Test School Group", UniqueSlug(), clock).Value;
        if (suspended)
        {
            organisation.Suspend();
        }

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<TenancyDbContext>();
            db.Organisations.Add(organisation);
            await db.SaveChangesAsync();
        }

        await using (var scope = factory.CreateTenantScope(organisation.Id, organisation.Slug))
        {
            var db = scope.ServiceProvider.GetRequiredService<TenancyDbContext>();
            var created = Enumerable.Range(1, branches)
                .Select(i => Branch.Create(organisation.Id, $"Campus {i}", $"C{i}", clock).Value)
                .ToArray();
            db.Branches.AddRange(created);
            await db.SaveChangesAsync();
            return new TestTenant(organisation.Id, organisation.Slug, [.. created.Select(b => b.Id)]);
        }
    }
}
