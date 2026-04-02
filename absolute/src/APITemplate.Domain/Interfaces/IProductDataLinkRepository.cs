using APITemplate.Domain.Entities;

namespace APITemplate.Domain.Interfaces;

/// <summary>
/// Repository contract for managing <see cref="ProductDataLink"/> join records between relational products and MongoDB product-data documents.
/// </summary>
public interface IProductDataLinkRepository
{
    /// <summary>
    /// Returns all links for the specified product, optionally including soft-deleted records.
    /// </summary>
    Task<IReadOnlyList<ProductDataLink>> ListByProductIdAsync(
        Guid productId,
        bool includeDeleted = false,
        CancellationToken ct = default
    );

    /// <summary>
    /// Returns links for the specified product IDs in a single query, optionally including soft-deleted records.
    /// </summary>
    Task<IReadOnlyDictionary<Guid, IReadOnlyList<ProductDataLink>>> ListByProductIdsAsync(
        IReadOnlyCollection<Guid> productIds,
        bool includeDeleted = false,
        CancellationToken ct = default
    );

    /// <summary>
    /// Returns <c>true</c> if at least one non-deleted link references the given product-data document.
    /// </summary>
    Task<bool> HasActiveLinksForProductDataAsync(
        Guid productDataId,
        CancellationToken ct = default
    );

    /// <summary>
    /// Soft-deletes all active links that reference the given product-data document.
    /// </summary>
    Task SoftDeleteActiveLinksForProductDataAsync(
        Guid productDataId,
        CancellationToken ct = default
    );
}
