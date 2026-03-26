using APITemplate.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace APITemplate.Infrastructure.Persistence.Configurations;

/// <summary>EF Core configuration for the <see cref="JobExecution"/> entity, including a progress check constraint and status index.</summary>
public sealed class JobExecutionConfiguration : IEntityTypeConfiguration<JobExecution>
{
    public void Configure(EntityTypeBuilder<JobExecution> builder)
    {
        builder.HasKey(j => j.Id);
        builder.ConfigureTenantAuditable();

        builder.Property(j => j.JobType).IsRequired().HasMaxLength(100);

        builder.Property(j => j.Status).IsRequired().HasMaxLength(20).HasConversion<string>();

        builder.Property(j => j.ProgressPercent).HasDefaultValue(0);

        builder.Property(j => j.Parameters).HasColumnType("text");

        builder.Property(j => j.CallbackUrl).HasMaxLength(2048);

        builder.Property(j => j.ResultPayload).HasColumnType("text");

        builder.Property(j => j.ErrorMessage).HasColumnType("text");

        builder
            .HasOne<Tenant>()
            .WithMany()
            .HasForeignKey(j => j.TenantId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(j => new { j.TenantId, j.Status });

        builder.ToTable(t =>
            t.HasCheckConstraint(
                "CK_JobExecutions_Progress",
                "\"ProgressPercent\" >= 0 AND \"ProgressPercent\" <= 100"
            )
        );
    }
}
