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
}
