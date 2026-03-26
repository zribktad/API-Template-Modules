namespace APITemplate.Domain.Entities;

/// <summary>
/// Keyless entity — no backing database table.
/// Used exclusively as a result type for the <c>get_product_category_stats</c> stored procedure.
/// EF Core maps each column from the SQL result set to these properties.
/// </summary>
public sealed class ProductCategoryStats
{
    public Guid CategoryId { get; set; }
    public string CategoryName { get; set; } = string.Empty;
    public long ProductCount { get; set; }
    public decimal AveragePrice { get; set; }
    public long TotalReviews { get; set; }
}
