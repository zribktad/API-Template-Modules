using BackgroundJobs.Shared;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SharedKernel.Infrastructure.Configurations;

namespace BackgroundJobs.Persistence.Configurations;

public sealed class JobExecutionConfiguration : IEntityTypeConfiguration<JobExecution>
{
    public void Configure(EntityTypeBuilder<JobExecution> builder)
    {
        builder.HasKey(j => j.Id);
        builder.ConfigureTenantAuditable();

        builder.Property(j => j.JobType).IsRequired().HasMaxLength(100);
        builder.Property(j => j.Status)
            .HasConversion<string>()
            .IsRequired()
            .HasMaxLength(20)
            .HasDefaultValue(JobStatus.Pending)
            .HasSentinel((JobStatus)(-1));
        builder.Property(j => j.ProgressPercent).HasDefaultValue(0);
        builder.Property(j => j.CallbackUrl).HasMaxLength(2048);

        builder.HasIndex(j => j.Status);
        builder.HasIndex(j => new { j.TenantId, j.SubmittedAtUtc });
    }
}


