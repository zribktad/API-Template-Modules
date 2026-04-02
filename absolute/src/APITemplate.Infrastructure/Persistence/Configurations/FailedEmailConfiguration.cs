using APITemplate.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace APITemplate.Infrastructure.Persistence.Configurations;

/// <summary>EF Core configuration for the <see cref="FailedEmail"/> entity, with composite indexes optimized for claim-based retry and expiration queries.</summary>
public sealed class FailedEmailConfiguration : IEntityTypeConfiguration<FailedEmail>
{
    public void Configure(EntityTypeBuilder<FailedEmail> builder)
    {
        builder.HasKey(e => e.Id);

        builder.Property(e => e.To).IsRequired().HasMaxLength(320);
        builder.Property(e => e.Subject).IsRequired().HasMaxLength(500);
        builder.Property(e => e.HtmlBody).IsRequired();
        builder.Property(e => e.LastError).HasMaxLength(FailedEmail.LastErrorMaxLength);
        builder.Property(e => e.TemplateName).HasMaxLength(100);

        builder
            .Property(e => e.CreatedAtUtc)
            .IsRequired()
            .HasColumnType("timestamp with time zone");

        builder.Property(e => e.LastAttemptAtUtc).HasColumnType("timestamp with time zone");
        builder.Property(e => e.ClaimedBy).HasMaxLength(200);
        builder.Property(e => e.ClaimedAtUtc).HasColumnType("timestamp with time zone");
        builder.Property(e => e.ClaimedUntilUtc).HasColumnType("timestamp with time zone");

        // Covers claim-based retry selection.
        builder.HasIndex(e => new
        {
            e.IsDeadLettered,
            e.RetryCount,
            e.ClaimedUntilUtc,
            e.LastAttemptAtUtc,
        });

        // Covers claim-based expiration/dead-letter selection.
        builder.HasIndex(e => new
        {
            e.IsDeadLettered,
            e.ClaimedUntilUtc,
            e.CreatedAtUtc,
        });
    }
}
