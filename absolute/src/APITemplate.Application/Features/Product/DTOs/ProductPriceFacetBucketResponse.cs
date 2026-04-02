namespace APITemplate.Application.Features.Product.DTOs;

/// <summary>
/// Represents a single price-range bucket in the product search facets, with a human-readable label and the count of matching products.
/// </summary>
public sealed record ProductPriceFacetBucketResponse(
    string Label,
    decimal MinPrice,
    decimal? MaxPrice,
    int Count
);
