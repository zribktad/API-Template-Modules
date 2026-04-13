using Identity.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SharedKernel.Infrastructure.Configurations;

namespace Identity.Persistence.Configurations;

public sealed class AppUserConfiguration : IEntityTypeConfiguration<AppUser>
{
    public void Configure(EntityTypeBuilder<AppUser> builder)
    {
        builder.HasKey(u => u.Id);
        builder.ConfigureTenantAuditable();

        builder.Property(u => u.Username).IsRequired().HasMaxLength(100);
        builder.Property(u => u.NormalizedUsername).IsRequired().HasMaxLength(100);
        builder
            .Property(u => u.Email)
            .HasConversion(e => e.Value, v => Email.FromPersistence(v))
            .IsRequired()
            .HasMaxLength(320);
        builder.Property(u => u.NormalizedEmail).IsRequired().HasMaxLength(320);
        builder.Property(u => u.KeycloakUserId).HasMaxLength(256);

        builder
            .HasIndex(u => u.KeycloakUserId)
            .IsUnique()
            .HasFilter("\"KeycloakUserId\" IS NOT NULL");

        builder.Property(u => u.IsActive).IsRequired().HasDefaultValue(true);

        builder
            .Property(u => u.ProvisioningStatus)
            .HasConversion<string>()
            .IsRequired()
            .HasMaxLength(32)
            .HasDefaultValue(ProvisioningStatus.Pending)
            .HasSentinel((ProvisioningStatus)(-1));

        builder
            .HasMany(u => u.Roles)
            .WithMany(r => r.Users)
            .UsingEntity(j => j.ToTable("AppUserRoles", "identity"));

        // FK to Tenant — no navigation property on Tenant side (module boundary)
        builder
            .HasOne<Tenant>()
            .WithMany()
            .HasForeignKey(u => u.TenantId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(u => new { u.TenantId, u.NormalizedUsername }).IsUnique();
        builder.HasIndex(u => new { u.TenantId, u.NormalizedEmail }).IsUnique();
    }
}
