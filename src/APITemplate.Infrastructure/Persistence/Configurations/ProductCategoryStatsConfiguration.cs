using APITemplate.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace APITemplate.Infrastructure.Persistence.Configurations;

/// <summary>
/// Registers <see cref="ProductCategoryStats"/> as a keyless entity.
/// HasNoKey() tells EF Core: this type has no primary key and no backing table.
/// It can only be materialised via FromSql() or raw SQL queries.
/// </summary>
public sealed class ProductCategoryStatsConfiguration : IEntityTypeConfiguration<ProductCategoryStats>
{
    public void Configure(EntityTypeBuilder<ProductCategoryStats> builder)
    {
        // No primary key, no table — result-set only.
        builder.HasNoKey();

        // ExcludeFromMigrations tells EF Core to skip this type when generating migrations.
        // The entity exists only as a materialisation target for FromSql() calls.
        builder.ToTable("ProductCategoryStats", t => t.ExcludeFromMigrations());
    }
}
