using APITemplate.Domain.Entities;
using APITemplate.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace APITemplate.Infrastructure.Persistence.Configurations;

/// <summary>EF Core configuration for the <see cref="AppUser"/> entity, defining constraints, indexes, and enum persistence.</summary>
public sealed class AppUserConfiguration : IEntityTypeConfiguration<AppUser>
{
    public void Configure(EntityTypeBuilder<AppUser> builder)
    {
        builder.HasKey(u => u.Id);
        builder.ConfigureTenantAuditable();

        builder.Property(u => u.Username).IsRequired().HasMaxLength(100);

        builder.Property(u => u.NormalizedUsername).IsRequired().HasMaxLength(100);

        builder.Property(u => u.Email).IsRequired().HasMaxLength(320);

        builder.Property(u => u.NormalizedEmail).IsRequired().HasMaxLength(320);

        builder.Property(u => u.KeycloakUserId).HasMaxLength(256);

        // Global uniqueness (not tenant-scoped): a Keycloak user ID is a system-wide identity.
        // A user can only belong to one tenant, so no TenantId prefix is needed.
        builder
            .HasIndex(u => u.KeycloakUserId)
            .IsUnique()
            .HasFilter("\"KeycloakUserId\" IS NOT NULL");

        builder.Property(u => u.IsActive).IsRequired().HasDefaultValue(true);

        // Persist the enum as a string (not an int) so the DB values stay readable and stable.
        // If we ever add/remove roles, the stored values don’t shift unexpectedly.
        builder
            .Property(u => u.Role)
            .HasConversion<string>()
            // Required: every user must have a role.
            .IsRequired()
            // Keep the underlying column size limited (and avoid long enum names).
            .HasMaxLength(32)
            // Default new users to a normal user role.
            .HasDefaultValue(UserRole.User)
            // Sentinel is used by the domain layer for a "missing" enum value.
            // It lets us detect cases where the value is invalid or not set.
            .HasSentinel((UserRole)(-1));

        builder
            .HasOne(u => u.Tenant)
            .WithMany(t => t.Users)
            .HasForeignKey(u => u.TenantId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(u => new { u.TenantId, u.NormalizedUsername }).IsUnique();
        builder.HasIndex(u => new { u.TenantId, u.NormalizedEmail }).IsUnique();
    }
}
