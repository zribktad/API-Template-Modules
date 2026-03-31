using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SharedKernel.Infrastructure.Configurations;

namespace Identity.Infrastructure.Persistence.Configurations;

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

        builder
            .HasIndex(u => u.KeycloakUserId)
            .IsUnique()
            .HasFilter("\"KeycloakUserId\" IS NOT NULL");

        builder.Property(u => u.IsActive).IsRequired().HasDefaultValue(true);

        builder
            .Property(u => u.Role)
            .HasConversion<string>()
            .IsRequired()
            .HasMaxLength(32)
            .HasDefaultValue(UserRole.User)
            .HasSentinel((UserRole)(-1));

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
