using System.ComponentModel.DataAnnotations;
using ErrorOr;
using ProductCatalog.GraphQL;
using SharedKernel.Application.Validation;
using ProductRepositoryContract = ProductCatalog.Interfaces.IProductRepository;
using SharedKernelErrorCatalog = SharedKernel.Application.Errors.ErrorCatalog;

namespace ProductCatalog.Features.Product.GetProducts;

/// <summary>Retrieves a filtered, sorted, and paged list of products together with search facets.</summary>
public sealed record GetProductsQuery(ProductFilter Filter);

/// <summary>Handles <see cref="GetProductsQuery" /> by fetching items, count, and facets from the repository.</summary>
public sealed class GetProductsQueryHandler
{
    public static ErrorOr<Success> Validate(GetProductsQuery query, IValidator validator)
    {
        IReadOnlyList<ValidationResult> failures = validator.Validate(query.Filter);
        if (failures.Count == 0)
            return Result.Success;
        return failures
            .Select(f => Error.Validation(
                SharedKernelErrorCatalog.General.ValidationFailed,
                f.ErrorMessage ?? "Validation failed.",
                new Dictionary<string, object> { ["propertyName"] = string.Join(", ", f.MemberNames) }))
            .ToList<Error>();
    }

    public static async Task<ErrorOr<ProductsResponse>> HandleAsync(
        GetProductsQuery request,
        ErrorOr<Success> validation,
        ProductRepositoryContract repository,
        CancellationToken ct
    )
    {
        if (validation.IsError)
            return validation.Errors;

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

    public static ProductPageResult PostProcess(ErrorOr<ProductsResponse> result)
    {
        ProductsResponse page = result.ToGraphQLResult();
        return new ProductPageResult(page.Page, page.Facets);
    }
}
