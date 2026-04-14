using ErrorOr;
using ProductCatalog.Features.Product.GetProducts;
using ProductEntity = ProductCatalog.Entities.Product;

namespace ProductCatalog.Interfaces;

/// <summary>
///     Domain-facing repository contract for products, extending the generic repository with product-specific filtered
///     queries and facet aggregations.
/// </summary>
public interface IProductRepository : IRepository<ProductEntity>
{
    /// <summary>Returns a single-query paged result of products matching the given filter.</summary>
    public Task<ErrorOr<PagedResponse<ProductResponse>>> GetPagedAsync(
        ProductFilter filter,
        CancellationToken ct = default
    );

    /// <summary>
    ///     Returns category facet counts for the current filter, ignoring any active category-ID constraints so all
    ///     categories remain selectable.
    /// </summary>
    public Task<IReadOnlyList<ProductCategoryFacetValue>> GetCategoryFacetsAsync(
        ProductFilter filter,
        CancellationToken ct = default
    );

    /// <summary>
    ///     Returns price-bucket facet counts for the current filter, ignoring any active price-range constraints so all
    ///     buckets remain selectable.
    /// </summary>
    public Task<IReadOnlyList<ProductPriceFacetBucketResponse>> GetPriceFacetsAsync(
        ProductFilter filter,
        CancellationToken ct = default
    );

    /// <summary>
    ///     Sets <c>CategoryId</c> to <c>null</c> on all products whose <c>CategoryId</c> is in
    ///     <paramref name="categoryIds" />.
    /// </summary>
    public Task ClearCategoryAsync(
        IReadOnlyCollection<Guid> categoryIds,
        CancellationToken ct = default
    );

    /// <summary>
    ///     Returns the IDs of all non-deleted products belonging to the specified tenant,
    ///     bypassing global query filters (lightweight ID-only projection).
    /// </summary>
    Task<IReadOnlyList<Guid>> GetNonDeletedIdsByTenantAsync(
        Guid tenantId,
        CancellationToken ct = default
    );

    /// <summary>
    ///     Bulk soft-deletes all non-deleted products for the specified tenant via a single
    ///     <c>ExecuteUpdateAsync</c> SQL statement (zero entity materialization).
    /// </summary>
    Task<int> BulkSoftDeleteByTenantAsync(
        Guid tenantId,
        Guid actorId,
        DateTime deletedAtUtc,
        CancellationToken ct = default
    );
}
