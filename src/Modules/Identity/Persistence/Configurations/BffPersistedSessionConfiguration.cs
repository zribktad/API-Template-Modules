using BuildingBlocks.Infrastructure.EFCore.Configurations;
using Identity.Auth.Security.Sessions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Identity.Persistence.Configurations;

public sealed class BffPersistedSessionConfiguration : IEntityTypeConfiguration<BffPersistedSession>
{
    public void Configure(EntityTypeBuilder<BffPersistedSession> builder)
    {
        builder.HasKey(s => s.Id);
        builder.ConfigureTenantAuditable();

        builder.Property(s => s.SessionId).IsRequired().HasMaxLength(128);
        builder.HasIndex(s => s.SessionId).IsUnique();

        builder.Property(s => s.UserId).IsRequired().HasMaxLength(256);
        builder.HasIndex(s => s.UserId);

        builder.Property(s => s.Subject).IsRequired().HasMaxLength(256);
        builder.HasIndex(s => s.Subject);

        builder
            .Property(s => s.Provider)
            .HasConversion<string>()
            .IsRequired()
            .HasMaxLength(32)
            .HasDefaultValue(BffProviderType.Keycloak)
            .HasSentinel((BffProviderType)(-1));

        builder
            .Property(s => s.Roles)
            .HasColumnType("jsonb")
            .IsRequired()
            .HasDefaultValueSql("'[]'::jsonb");

        builder.Property(s => s.Email).HasMaxLength(320);
        builder.Property(s => s.DisplayName).HasMaxLength(256);

        builder.Property(s => s.EncryptedAccessToken).IsRequired();
        builder.Property(s => s.EncryptedRefreshToken).IsRequired();

        builder
            .Property(s => s.AccessTokenExpiresAtUtc)
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder.Property(s => s.RefreshTokenExpiresAtUtc).HasColumnType("timestamp with time zone");

        builder
            .Property(s => s.CreatedAtUtc)
            .HasColumnName("SessionCreatedAtUtc")
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder
            .Property(s => s.LastSeenAtUtc)
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder
            .Property(s => s.LastRefreshedAtUtc)
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder
            .Property(s => s.Status)
            .HasConversion<string>()
            .IsRequired()
            .HasMaxLength(32)
            .HasDefaultValue(BffSessionStatus.Active)
            .HasSentinel((BffSessionStatus)(-1));

        builder.Property(s => s.RevokedAtUtc).HasColumnType("timestamp with time zone");

        builder.Property(s => s.RevocationReason).HasConversion<string>().HasMaxLength(64);

        builder.HasIndex(s => new { s.Status, s.LastSeenAtUtc });
        builder.HasIndex(s => s.CreatedAtUtc);
    }
}
