using ErrorOr;
using ProductRepositoryContract = ProductCatalog.Features.Product.Repositories.IProductRepository;

namespace ProductCatalog.Features.Product;

/// <summary>Retrieves a filtered, sorted, and paged list of products together with search facets.</summary>
public sealed record GetProductsQuery(ProductFilter Filter);

/// <summary>Handles <see cref="GetProductsQuery"/> by fetching items, count, and facets from the repository.</summary>
public sealed class GetProductsQueryHandler
{
    public static async Task<ErrorOr<ProductsResponse>> HandleAsync(
        GetProductsQuery request,
        ProductRepositoryContract repository,
        CancellationToken ct
    )
    {
        ErrorOr<PagedResponse<ProductResponse>> page = await repository.GetPagedAsync(
            request.Filter,
            ct
        );
        if (page.IsError)
            return page.Errors;

        IReadOnlyList<ProductCategoryFacetValue> categoryFacets =
            await repository.GetCategoryFacetsAsync(request.Filter, ct);
        IReadOnlyList<ProductPriceFacetBucketResponse> priceFacets =
            await repository.GetPriceFacetsAsync(request.Filter, ct);

        return new ProductsResponse(
            page.Value,
            new ProductSearchFacetsResponse(categoryFacets, priceFacets)
        );
    }
}

