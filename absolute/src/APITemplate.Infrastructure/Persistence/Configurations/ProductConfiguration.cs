using APITemplate.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace APITemplate.Infrastructure.Persistence.Configurations;

/// <summary>EF Core configuration for the <see cref="Product"/> entity, including price precision and a full-text search GIN index.</summary>
public sealed class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> builder)
    {
        builder.HasKey(p => p.Id);
        builder.ConfigureTenantAuditable();

        builder.Property(p => p.Name).IsRequired().HasMaxLength(200);

        builder.Property(p => p.Description).HasMaxLength(1000);

        builder.Property(p => p.Price).HasPrecision(18, 2);

        builder
            .HasOne(p => p.Category)
            .WithMany(c => c.Products)
            .HasForeignKey(p => p.CategoryId)
            .OnDelete(DeleteBehavior.SetNull);

        builder
            .HasOne<Tenant>()
            .WithMany()
            .HasForeignKey(p => p.TenantId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(p => new { p.TenantId, p.Name });
        builder
            .HasIndex(p => new { p.Name, p.Description })
            .HasMethod("GIN")
            .IsTsVectorExpressionIndex("english");
    }
}
