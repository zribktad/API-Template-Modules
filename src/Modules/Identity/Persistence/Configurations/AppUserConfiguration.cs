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

        builder.OwnsOne(u => u.Username, b =>
        {
            b.Property(x => x.Value).HasColumnName("Username").IsRequired().HasMaxLength(AppUser.UsernameMaxLength);
            b.Property(x => x.Normalized).HasColumnName("NormalizedUsername").IsRequired().HasMaxLength(AppUser.UsernameMaxLength);
        });
        builder.OwnsOne(u => u.Email, b =>
        {
            b.Property(x => x.Value).HasColumnName("Email").IsRequired().HasMaxLength(AppUser.EmailMaxLength);
            b.Property(x => x.Normalized).HasColumnName("NormalizedEmail").IsRequired().HasMaxLength(AppUser.EmailMaxLength);
        });

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

    }
}
