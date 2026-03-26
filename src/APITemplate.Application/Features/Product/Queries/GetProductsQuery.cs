using ErrorOr;

namespace APITemplate.Application.Features.Product;

/// <summary>Retrieves a filtered, sorted, and paged list of products together with search facets.</summary>
public sealed record GetProductsQuery(ProductFilter Filter);

/// <summary>Handles <see cref="GetProductsQuery"/> by fetching items, count, and facets from the repository.</summary>
public sealed class GetProductsQueryHandler
{
    public static async Task<ErrorOr<ProductsResponse>> HandleAsync(
        GetProductsQuery request,
        IProductRepository repository,
        CancellationToken ct
    )
    {
        var page = await repository.GetPagedAsync(request.Filter, ct);
        var categoryFacets = await repository.GetCategoryFacetsAsync(request.Filter, ct);
        var priceFacets = await repository.GetPriceFacetsAsync(request.Filter, ct);

        return new ProductsResponse(
            page,
            new ProductSearchFacetsResponse(categoryFacets, priceFacets)
        );
    }
}
