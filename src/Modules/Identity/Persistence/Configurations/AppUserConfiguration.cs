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

        builder
            .Property(u => u.DbEmail)
            .HasColumnName("Email")
            .IsRequired()
            .HasMaxLength(AppUser.EmailMaxLength);
        builder
            .Property(u => u.DbNormalizedEmail)
            .HasColumnName("NormalizedEmail")
            .IsRequired()
            .HasMaxLength(AppUser.EmailMaxLength);
        builder
            .Property(u => u.DbUsername)
            .HasColumnName("Username")
            .IsRequired()
            .HasMaxLength(AppUser.UsernameMaxLength);
        builder
            .Property(u => u.DbNormalizedUsername)
            .HasColumnName("NormalizedUsername")
            .IsRequired()
            .HasMaxLength(AppUser.UsernameMaxLength);

        builder.Ignore(u => u.Email);
        builder.Ignore(u => u.Username);

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

        builder
            .HasOne<Tenant>()
            .WithMany()
            .HasForeignKey(u => u.TenantId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(u => new { u.TenantId, u.DbNormalizedEmail }).IsUnique();
        builder.HasIndex(u => new { u.TenantId, u.DbNormalizedUsername }).IsUnique();

        builder.HasIndex(u => u.DbNormalizedEmail, "IX_Users_NormalizedEmail");
        builder.HasIndex(u => u.DbNormalizedUsername, "IX_Users_NormalizedUsername");

        builder
            .HasIndex(u => u.DbNormalizedUsername, "IX_Users_NormalizedUsername_Trgm")
            .HasMethod("GIN")
            .HasOperators("gin_trgm_ops");
    }
}
