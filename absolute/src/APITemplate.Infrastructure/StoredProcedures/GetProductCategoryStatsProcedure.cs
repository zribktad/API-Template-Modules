using APITemplate.Domain.Entities;
using APITemplate.Domain.Interfaces;

namespace APITemplate.Infrastructure.StoredProcedures;

/// <summary>
/// Calls the <c>get_product_category_stats(p_category_id, p_tenant_id)</c> PostgreSQL function.
///
/// Result columns returned by the function:
///   category_id, category_name, product_count, average_price, total_reviews
///
/// EF Core maps each column to the corresponding property on
/// <see cref="ProductCategoryStats"/> by name (case-insensitive).
/// </summary>
public sealed record GetProductCategoryStatsProcedure(Guid CategoryId, Guid TenantId)
    : IStoredProcedure<ProductCategoryStats>
{
    public FormattableString ToSql() =>
        $"SELECT * FROM get_product_category_stats({CategoryId}, {TenantId})";
}
