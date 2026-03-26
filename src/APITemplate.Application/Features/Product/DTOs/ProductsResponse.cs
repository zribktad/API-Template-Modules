namespace APITemplate.Application.Features.Product.DTOs;

/// <summary>
/// Combines a paged list of products with their associated search facets in a single response envelope.
/// </summary>
public sealed record ProductsResponse(
    PagedResponse<ProductResponse> Page,
    ProductSearchFacetsResponse Facets
) : IPagedItems<ProductResponse>, IHasFacets<ProductSearchFacetsResponse>;
