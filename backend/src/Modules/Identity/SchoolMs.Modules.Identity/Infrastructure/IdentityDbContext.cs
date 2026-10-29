using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SchoolMs.Infrastructure.Persistence;
using SchoolMs.Modules.Identity.Domain;
using SchoolMs.SharedKernel;

namespace SchoolMs.Modules.Identity.Infrastructure;

/// <summary>
/// Users, plus the ASP.NET Core Data Protection key ring. The key ring protects session cookies, antiforgery
/// tokens and invitation/reset links, so it must be shared by every API instance and survive restarts.
/// </summary>
internal sealed class IdentityDbContext(DbContextOptions<IdentityDbContext> options, ITenantContext tenantContext)
    : ModuleDbContext(options, tenantContext), IDataProtectionKeyContext
{
    public const string SchemaName = "identity";

    public DbSet<User> Users => Set<User>();

    public DbSet<DataProtectionKey> DataProtectionKeys => Set<DataProtectionKey>();

    protected override string Schema => SchemaName;
}

internal sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("users");
        builder.HasKey(u => u.Id);
        builder.Property(u => u.Id).ValueGeneratedNever();
        builder.Property(u => u.FullName).HasMaxLength(200).IsRequired();
        builder.Property(u => u.Email).HasMaxLength(User.MaxEmailLength).IsRequired();
        builder.Property(u => u.NormalizedEmail).HasMaxLength(User.MaxEmailLength).IsRequired();
        builder.Property(u => u.PhoneNumber).HasMaxLength(16);
        builder.Property(u => u.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(u => u.PasswordHash).HasMaxLength(200);
        builder.Property(u => u.SecurityStamp).HasMaxLength(64).IsRequired();
        builder.Property(u => u.ConcurrencyStamp).HasMaxLength(64).IsRequired().IsConcurrencyToken();
        builder.Property(u => u.EmailConfirmed);
        builder.Property(u => u.LockoutEnabled);
        builder.Property(u => u.LockoutEnd);
        builder.Property(u => u.AccessFailedCount);

        // Unique per organisation: accounts in different organisations are unrelated (ADR-0003).
        builder.HasIndex(u => new { u.OrganisationId, u.NormalizedEmail }).IsUnique();
        builder.HasIndex(u => new { u.OrganisationId, u.PhoneNumber }).IsUnique();

        builder.Ignore(u => u.IsActive);
        builder.Ignore(u => u.DomainEvents);
    }
}

internal sealed class DataProtectionKeyConfiguration : IEntityTypeConfiguration<DataProtectionKey>
{
    public void Configure(EntityTypeBuilder<DataProtectionKey> builder) => builder.ToTable("data_protection_keys");
}
