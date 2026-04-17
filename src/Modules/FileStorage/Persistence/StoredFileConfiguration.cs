using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SharedKernel.Infrastructure.Configurations;

namespace FileStorage.Persistence;

internal sealed class StoredFileConfiguration : IEntityTypeConfiguration<StoredFile>
{
    public void Configure(EntityTypeBuilder<StoredFile> builder)
    {
        builder.ToTable("stored_files");

        builder.ConfigureTenantAuditable();

        builder.Property(x => x.OriginalFileName).IsRequired().HasMaxLength(255);
        builder.Property(x => x.Sha256).IsRequired().HasMaxLength(64).IsFixedLength();
        builder.Property(x => x.BackendKey).IsRequired().HasMaxLength(32);
        builder.Property(x => x.ContentType).IsRequired().HasMaxLength(100);
        builder.Property(x => x.Description).HasMaxLength(500);

        // Compound index supports refcount query: COUNT(*) WHERE Sha256=@s AND TenantId=@t AND !IsDeleted.
        builder.HasIndex(x => new { x.Sha256, x.TenantId });
    }
}
