using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using SchoolMs.SharedKernel;

namespace SchoolMs.Infrastructure.Persistence;

/// <summary>
/// Base class for every module's DbContext. Each module owns one PostgreSQL schema, and every
/// <see cref="ITenantOwned"/> entity automatically gets the tenant query filter (ADR-0002).
/// </summary>
public abstract class ModuleDbContext(DbContextOptions options, ITenantContext tenantContext) : DbContext(options)
{
    public const string TenantFilter = "Tenant";

    /// <summary>The PostgreSQL schema owned by this module, e.g. "tenancy".</summary>
    protected abstract string Schema { get; }

    /// <summary>Read by the query filter on every query; EF Core re-evaluates it per context instance.</summary>
    protected Guid? CurrentOrganisationId => tenantContext.OrganisationId;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.ApplyConfigurationsFromAssembly(GetType().Assembly);
        ApplyTenantFilters(modelBuilder);
    }

    private void ApplyTenantFilters(ModelBuilder modelBuilder)
    {
        var tenantTypes = modelBuilder.Model.GetEntityTypes()
            .Where(t => t.BaseType is null && typeof(ITenantOwned).IsAssignableFrom(t.ClrType))
            .Select(t => t.ClrType)
            .ToList();

        foreach (var clrType in tenantTypes)
        {
            // e => (Guid?)e.OrganisationId == this.CurrentOrganisationId
            // A null tenant matches nothing, so a missing tenant hides all tenant data.
            var entity = Expression.Parameter(clrType, "e");
            var body = Expression.Equal(
                Expression.Convert(Expression.Property(entity, nameof(ITenantOwned.OrganisationId)), typeof(Guid?)),
                Expression.Property(Expression.Constant(this), nameof(CurrentOrganisationId)));

            modelBuilder.Entity(clrType).HasQueryFilter(TenantFilter, Expression.Lambda(body, entity));
        }
    }
}
