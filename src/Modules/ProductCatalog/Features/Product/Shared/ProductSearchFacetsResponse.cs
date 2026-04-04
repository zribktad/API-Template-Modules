namespace ProductCatalog.Features.Product.Shared;

/// <summary>
///     Aggregates all facet data returned alongside a product search result, enabling client-side filter refinement.
/// </summary>
public sealed record ProductSearchFacetsResponse(
    IReadOnlyCollection<ProductCategoryFacetValue> Categories,
    IReadOnlyCollection<ProductPriceFacetBucketResponse> PriceBuckets
);
