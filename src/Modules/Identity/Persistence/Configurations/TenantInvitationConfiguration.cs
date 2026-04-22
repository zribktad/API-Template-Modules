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

        builder.OwnsOne(i => i.Email, b =>
        {
            b.Property(x => x.Value).HasColumnName("Email").IsRequired().HasMaxLength(AppUser.EmailMaxLength);
            b.Property(x => x.Normalized).HasColumnName("NormalizedEmail").IsRequired().HasMaxLength(AppUser.EmailMaxLength);
        });

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

        // FK to Tenant — no navigation property on Tenant side (module boundary)
        builder
            .HasOne<Tenant>()
            .WithMany()
            .HasForeignKey(i => i.TenantId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(i => i.TokenHash);
    }
}
