using Reviews.GraphQL.Models;

namespace Reviews.GraphQL;

/// <summary>
///     Maps GraphQL input types to application-layer filter records.
/// </summary>
internal static class QueryInputExtensions
{
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
