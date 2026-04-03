using ProductCatalog.Entities;

namespace ProductCatalog.Interfaces;

/// <summary>
/// Repository contract for <see cref="Category"/> entities, extending the generic repository with category-specific queries.
/// </summary>
public interface ICategoryRepository : IRepository<Category>
{
    /// <summary>
    /// Calls the <c>get_product_category_stats(p_category_id, p_tenant_id)</c> PostgreSQL stored procedure
    /// and returns aggregated statistics for the given category within the current tenant context.
    /// Returns <c>null</c> when no category with the specified ID exists.
    /// </summary>
    Task<ProductCategoryStats?> GetStatsByIdAsync(Guid categoryId, CancellationToken ct = default);
}



