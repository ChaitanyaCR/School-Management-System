using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using SchoolMs.SharedKernel;

namespace SchoolMs.Infrastructure.Persistence;

/// <summary>Thrown when code tries to write another tenant's data. Always a bug, never a user error.</summary>
public sealed class TenantIsolationException(string message) : InvalidOperationException(message);

/// <summary>
/// Refuses to save any tenant-owned entity that does not belong to the current tenant, including attempts to
/// move an existing row to another organisation. The database RLS policy enforces the same rule independently.
/// </summary>
internal sealed class TenantGuardInterceptor(ITenantContext tenantContext) : SaveChangesInterceptor
{
    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        Guard(eventData.Context);
        return result;
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        Guard(eventData.Context);
        return ValueTask.FromResult(result);
    }

    private void Guard(DbContext? context)
    {
        if (context is null)
        {
            return;
        }

        foreach (var entry in context.ChangeTracker.Entries<ITenantOwned>())
        {
            if (entry.State is not (EntityState.Added or EntityState.Modified or EntityState.Deleted))
            {
                continue;
            }

            var current = tenantContext.OrganisationId
                ?? throw new TenantIsolationException($"Cannot save {entry.Metadata.ClrType.Name} without a tenant.");

            var original = entry.State == EntityState.Added
                ? entry.Entity.OrganisationId
                : entry.Property(e => e.OrganisationId).OriginalValue;

            if (entry.Entity.OrganisationId != current || original != current)
            {
                throw new TenantIsolationException($"Cannot save {entry.Metadata.ClrType.Name} belonging to another organisation.");
            }
        }
    }
}
