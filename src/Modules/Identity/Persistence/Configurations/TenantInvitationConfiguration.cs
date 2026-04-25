using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SharedKernel.Infrastructure.Configurations;

namespace Identity.Persistence.Configurations;

public sealed class TenantInvitationConfiguration : IEntityTypeConfiguration<TenantInvitation>
{
    public void Configure(EntityTypeBuilder<TenantInvitation> builder)
    {
        builder.HasKey(i => i.Id);
        builder.ConfigureTenantAuditable();

        builder
            .Property(i => i.DbEmail)
            .HasColumnName("Email")
            .IsRequired()
            .HasMaxLength(TenantInvitation.EmailMaxLength);
        builder
            .Property(i => i.DbNormalizedEmail)
            .HasColumnName("NormalizedEmail")
            .IsRequired()
            .HasMaxLength(TenantInvitation.EmailMaxLength);

        builder.Ignore(i => i.Email);

        builder.Property(i => i.TokenHash).IsRequired().HasMaxLength(128);

        builder
            .Property(i => i.ExpiresAtUtc)
            .IsRequired()
            .HasColumnType("timestamp with time zone");

        builder
            .Property(i => i.Status)
            .HasConversion<string>()
            .IsRequired()
            .HasMaxLength(32)
            .HasDefaultValue(InvitationStatus.Pending)
            .HasSentinel((InvitationStatus)(-1));

        builder
            .HasOne<Tenant>()
            .WithMany()
            .HasForeignKey(i => i.TenantId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(i => i.TokenHash);

        builder.HasIndex(i => new { i.TenantId, i.DbNormalizedEmail });
    }
}
