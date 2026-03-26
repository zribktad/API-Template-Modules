using APITemplate.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace APITemplate.Infrastructure.Persistence.Configurations;

/// <summary>EF Core configuration for the <see cref="StoredFile"/> entity, mapped to the <c>ExampleFiles</c> table.</summary>
public sealed class StoredFileConfiguration : IEntityTypeConfiguration<StoredFile>
{
    public void Configure(EntityTypeBuilder<StoredFile> builder)
    {
        // Table named 'ExampleFiles' as part of the examples/showcase feature
        builder.ToTable("ExampleFiles");

        builder.HasKey(e => e.Id);
        builder.ConfigureTenantAuditable();

        builder.Property(e => e.OriginalFileName).IsRequired().HasMaxLength(255);

        builder.Property(e => e.StoragePath).IsRequired().HasMaxLength(500);

        builder.Property(e => e.ContentType).IsRequired().HasMaxLength(100);

        builder.Property(e => e.Description).HasMaxLength(1000);

        builder
            .HasOne<Tenant>()
            .WithMany()
            .HasForeignKey(e => e.TenantId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
