namespace APITemplate.Api.GraphQL.Models;

/// <summary>
/// GraphQL return type that combines a paginated product result set with search facets,
/// implementing both <see cref="IPagedItems{T}"/> and <see cref="IHasFacets{T}"/> contracts.
/// </summary>
public sealed record ProductPageResult(
    PagedResponse<ProductResponse> Page,
    ProductSearchFacetsResponse Facets
) : IPagedItems<ProductResponse>, IHasFacets<ProductSearchFacetsResponse>;
