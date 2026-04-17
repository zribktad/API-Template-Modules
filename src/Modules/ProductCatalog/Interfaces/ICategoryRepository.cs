namespace ProductCatalog.Interfaces;

/// <summary>
///     Repository contract for <see cref="Category" /> entities, extending the generic repository with category-specific
///     queries.
/// </summary>
public interface ICategoryRepository : IRepository<Category>
{
    /// <summary>
    ///     Calls the <c>get_product_category_stats(p_category_id, p_tenant_id)</c> PostgreSQL stored procedure
    ///     and returns aggregated statistics for the given category within the current tenant context.
    ///     Returns <c>null</c> when no category with the specified ID exists.
    /// </summary>
    public Task<ProductCategoryStats?> GetStatsByIdAsync(
        Guid categoryId,
        CancellationToken ct = default
    );

    /// <summary>
    ///     Bulk soft-deletes all non-deleted categories for the specified tenant via a single
    ///     <c>ExecuteUpdateAsync</c> SQL statement (zero entity materialization).
    /// </summary>
    Task<int> BulkSoftDeleteByTenantAsync(
        Guid tenantId,
        Guid actorId,
        DateTime deletedAtUtc,
        CancellationToken ct = default
    );

    /// <summary>
    ///     Bulk soft-deletes the specified categories by ID via a single <c>ExecuteUpdateAsync</c>
    ///     SQL statement (zero entity materialization). Only non-deleted categories are affected.
    ///     No <c>CategorySoftDeletedNotification</c> is published — no module currently subscribes
    ///     to category deletion events; add the notification if a cross-module consumer is introduced.
    /// </summary>
    Task<int> BulkSoftDeleteByIdsAsync(
        IReadOnlyCollection<Guid> ids,
        Guid actorId,
        DateTime deletedAtUtc,
        CancellationToken ct = default
    );
}
