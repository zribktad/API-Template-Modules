using System.ComponentModel.DataAnnotations;

namespace Reviews.Features;

/// <summary>
///     Filter parameters for querying product reviews, supporting filtering by product, user, rating range, date range,
///     sorting, and pagination.
/// </summary>
public sealed record ProductReviewFilter(
    Guid? ProductId = null,
    Guid? UserId = null,
    [property: Range(1, 5, ErrorMessage = "MinRating must be between 1 and 5.")]
        int? MinRating = null,
    [property: Range(1, 5, ErrorMessage = "MaxRating must be between 1 and 5.")]
    [property: GreaterThanOrEqualToProperty(
        "MinRating",
        ErrorMessage = "MaxRating must be greater than or equal to MinRating."
    )]
        int? MaxRating = null,
    DateTime? CreatedFrom = null,
    [property: GreaterThanOrEqualToProperty(
        "CreatedFrom",
        ErrorMessage = "CreatedTo must be greater than or equal to CreatedFrom."
    )]
        DateTime? CreatedTo = null,
    [property: CaseInsensitiveAllowedValues(
        ProductReviewSortFields.RatingToken,
        ProductReviewSortFields.CreatedAtToken,
        ErrorMessage = "SortBy must be one of: Rating, CreatedAt."
    )]
        string? SortBy = null,
    [property: SortDirection] string? SortDirection = null,
    int PageNumber = 1,
    int PageSize = PaginationFilter.DefaultPageSize
) : PaginationFilter(PageNumber, PageSize), IDateRangeFilter, ISortableFilter;
