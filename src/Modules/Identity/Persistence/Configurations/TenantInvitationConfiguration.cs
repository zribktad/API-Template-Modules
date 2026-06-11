using BuildingBlocks.Infrastructure.EFCore.Configurations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Identity.Persistence.Configurations;

public sealed class TenantInvitationConfiguration : IEntityTypeConfiguration<TenantInvitation>
{
    /// <summary>
    ///     Partial unique index guaranteeing at most one <see cref="InvitationStatus.Pending" /> invitation per
    ///     (tenant, normalised email). Backs the duplicate-pending check against the concurrent check-then-insert
    ///     race; the unique-violation is translated to a domain error.
    /// </summary>
    public const string PendingInvitationIndexName =
        "IX_TenantInvitations_TenantId_NormalizedEmail_Pending";

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

        // Partial unique index: one Pending invitation per (tenant, normalised email). Status is
        // persisted as text (HasConversion<string>), so the filter compares the string literal.
        builder
            .HasIndex(i => new { i.TenantId, i.DbNormalizedEmail })
            .HasDatabaseName(PendingInvitationIndexName)
            .IsUnique()
            .HasFilter("\"Status\" = 'Pending'");
    }
}
