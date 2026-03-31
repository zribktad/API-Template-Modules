using APITemplate.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace APITemplate.Infrastructure.Persistence.Configurations;

/// <summary>EF Core configuration for the <see cref="Category"/> entity, including a full-text search GIN index.</summary>
public sealed class CategoryConfiguration : IEntityTypeConfiguration<Category>
{
    public void Configure(EntityTypeBuilder<Category> builder)
    {
        builder.HasKey(c => c.Id);
        builder.ConfigureTenantAuditable();

        builder.Property(c => c.Name).IsRequired().HasMaxLength(100);

        builder.Property(c => c.Description).HasMaxLength(500);

        builder
            .HasOne<Tenant>()
            .WithMany()
            .HasForeignKey(c => c.TenantId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(c => new { c.TenantId, c.Name }).IsUnique();
        builder
            .HasIndex(c => new { c.Name, c.Description })
            .HasMethod("GIN")
            .IsTsVectorExpressionIndex("english");
    }
}
