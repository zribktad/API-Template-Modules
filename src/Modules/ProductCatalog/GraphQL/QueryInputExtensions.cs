using ProductCatalog.Features.Category.GetCategories;
using ProductCatalog.Features.Product.GetProducts;

namespace ProductCatalog.GraphQL;

/// <summary>
///     Maps GraphQL input types to application-layer filter records.
/// </summary>
internal static class QueryInputExtensions
{
    internal static ProductFilter ToFilter(this ProductQueryInput input)
    {
        return new ProductFilter(
            input.Name,
            input.Description,
            input.MinPrice,
            input.MaxPrice,
            input.CreatedFrom,
            input.CreatedTo,
            input.SortBy,
            input.SortDirection,
            input.PageNumber,
            input.PageSize,
            input.Query,
            input.CategoryIds
        );
    }

    internal static CategoryFilter ToFilter(this CategoryQueryInput input)
    {
        return new CategoryFilter(
            input.Query,
            input.SortBy,
            input.SortDirection,
            input.PageNumber,
            input.PageSize
        );
    }

    internal static ProductReviewFilter ToFilter(this ProductReviewQueryInput input)
    {
        return new ProductReviewFilter(
            input.ProductId,
            input.UserId,
            input.MinRating,
            input.MaxRating,
            input.CreatedFrom,
            input.CreatedTo,
            input.SortBy,
            input.SortDirection,
            input.PageNumber,
            input.PageSize
        );
    }
}
