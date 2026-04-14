namespace ProductCatalog.Interfaces;

/// <summary>
///     Repository contract for managing <see cref="ProductDataLink" /> join records between relational products and
///     MongoDB product-data documents.
/// </summary>
public interface IProductDataLinkRepository
{
    /// <summary>
    ///     Returns all links for the specified product, optionally including soft-deleted records.
    /// </summary>
    public Task<IReadOnlyList<ProductDataLink>> ListByProductIdAsync(
        Guid productId,
        bool includeDeleted = false,
        CancellationToken ct = default
    );

    /// <summary>
    ///     Returns links for the specified product IDs in a single query, optionally including soft-deleted records.
    /// </summary>
    public Task<IReadOnlyDictionary<Guid, IReadOnlyList<ProductDataLink>>> ListByProductIdsAsync(
        IReadOnlyCollection<Guid> productIds,
        bool includeDeleted = false,
        CancellationToken ct = default
    );

    /// <summary>
    ///     Returns <c>true</c> if at least one non-deleted link references the given product-data document.
    /// </summary>
    public Task<bool> HasActiveLinksForProductDataAsync(
        Guid productDataId,
        CancellationToken ct = default
    );

    /// <summary>
    ///     Soft-deletes all active links that reference the given product-data document.
    /// </summary>
    public Task SoftDeleteActiveLinksForProductDataAsync(
        Guid productDataId,
        CancellationToken ct = default
    );

    /// <summary>
    ///     Stages the given already-tracked links for removal so they are soft-deleted
    ///     when the unit of work commits.
    /// </summary>
    public void DeleteRange(IReadOnlyCollection<ProductDataLink> links);

    /// <summary>
    ///     Bulk soft-deletes all non-deleted product data links for the specified tenant via a single
    ///     <c>ExecuteUpdateAsync</c> SQL statement (zero entity materialization).
    /// </summary>
    Task<int> BulkSoftDeleteByTenantAsync(
        Guid tenantId,
        Guid actorId,
        DateTime deletedAtUtc,
        CancellationToken ct = default
    );
}
