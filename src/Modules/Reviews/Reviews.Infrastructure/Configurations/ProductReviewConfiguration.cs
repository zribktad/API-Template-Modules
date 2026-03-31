using Reviews.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Reviews.Infrastructure.Configurations;

/// <summary>EF Core configuration for the <see cref="ProductReview"/> entity.</summary>
public sealed class ProductReviewConfiguration : IEntityTypeConfiguration<ProductReview>
{
    public void Configure(EntityTypeBuilder<ProductReview> builder)
    {
        builder.HasKey(r => r.Id);
        builder.ConfigureTenantAuditable();

        builder.Property(r => r.Comment).HasMaxLength(2000);

        builder.Property(r => r.Rating).IsRequired();

        builder.HasIndex(r => new { r.TenantId, r.ProductId });
    }
}


