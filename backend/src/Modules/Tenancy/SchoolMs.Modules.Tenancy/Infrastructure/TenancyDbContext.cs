using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SchoolMs.Infrastructure.Persistence;
using SchoolMs.Modules.Tenancy.Domain;
using SchoolMs.SharedKernel;

namespace SchoolMs.Modules.Tenancy.Infrastructure;

internal sealed class TenancyDbContext(DbContextOptions<TenancyDbContext> options, ITenantContext tenantContext)
    : ModuleDbContext(options, tenantContext)
{
    public const string SchemaName = "tenancy";

    public DbSet<Organisation> Organisations => Set<Organisation>();

    public DbSet<Branch> Branches => Set<Branch>();

    protected override string Schema => SchemaName;
}

internal sealed class OrganisationConfiguration : IEntityTypeConfiguration<Organisation>
{
    public void Configure(EntityTypeBuilder<Organisation> builder)
    {
        builder.ToTable("organisations");
        builder.HasKey(o => o.Id);
        builder.Property(o => o.Id).ValueGeneratedNever();
        builder.Property(o => o.Name).HasMaxLength(200).IsRequired();
        builder.Property(o => o.Slug).HasMaxLength(30).IsRequired();
        builder.HasIndex(o => o.Slug).IsUnique();
        builder.Property(o => o.Status).HasConversion<string>().HasMaxLength(20);
        builder.Ignore(o => o.IsActive);
        builder.Ignore(o => o.DomainEvents);
    }
}

internal sealed class BranchConfiguration : IEntityTypeConfiguration<Branch>
{
    public void Configure(EntityTypeBuilder<Branch> builder)
    {
        builder.ToTable("branches");
        builder.HasKey(b => b.Id);
        builder.Property(b => b.Id).ValueGeneratedNever();
        builder.Property(b => b.Name).HasMaxLength(200).IsRequired();
        builder.Property(b => b.Code).HasMaxLength(10).IsRequired();
        builder.HasIndex(b => new { b.OrganisationId, b.Code }).IsUnique();
        builder.HasOne<Organisation>().WithMany().HasForeignKey(b => b.OrganisationId).OnDelete(DeleteBehavior.Restrict);
        builder.Ignore(b => b.DomainEvents);
    }
}
