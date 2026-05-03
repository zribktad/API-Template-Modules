using BuildingBlocks.Infrastructure.EFCore.Configurations;
using FileStorage.Domain.Sagas;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FileStorage.Persistence;

internal sealed class FileUploadSagaConfiguration : IEntityTypeConfiguration<FileUploadSaga>
{
    public void Configure(EntityTypeBuilder<FileUploadSaga> builder)
    {
        builder.ToTable("file_upload_sagas");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasMaxLength(64).IsRequired();

        builder.Property(x => x.TenantId).IsRequired();
        builder.Property(x => x.Sha256).IsRequired().HasMaxLength(64).IsFixedLength();
        builder.Property(x => x.SizeBytes).IsRequired();
        builder.Property(x => x.OriginalFileName).IsRequired().HasMaxLength(255);
        builder.Property(x => x.StagingPath).IsRequired().HasMaxLength(1000);
        builder.Property(x => x.BackendKey).IsRequired().HasMaxLength(32);
        builder
            .Property(x => x.Status)
            .IsRequired()
            .HasConversion(v => v.ToString(), v => Enum.Parse<FileUploadStatus>(v))
            .HasMaxLength(16);
        builder.Property(x => x.CreatedAtUtc).IsRequired();
        builder.Property(x => x.CommitDeadlineUtc).IsRequired();

        builder.HasIndex(x => x.Status);
        builder.HasIndex(x => x.CommitDeadlineUtc);

        builder.ConfigurePostgresXminConcurrency();
    }
}
