using System.ComponentModel.DataAnnotations;

namespace Reviews.Features;

/// <summary>
///     Filter parameters for querying product reviews, supporting filtering by product, user, rating range, date range,
///     sorting, and pagination.
/// </summary>
public sealed record ProductReviewFilter : PaginationFilter, IDateRangeFilter, ISortableFilter
{
    public Guid? ProductId { get; init; }

    public Guid? UserId { get; init; }

    [Range(1, 5, ErrorMessage = "MinRating must be between 1 and 5.")]
    public int? MinRating { get; init; }

    [Range(1, 5, ErrorMessage = "MaxRating must be between 1 and 5.")]
    [GreaterThanOrEqualToProperty(
        nameof(MinRating),
        ErrorMessage = "MaxRating must be greater than or equal to MinRating."
    )]
    public int? MaxRating { get; init; }

    public DateTime? CreatedFrom { get; init; }

    [GreaterThanOrEqualToProperty(
        nameof(CreatedFrom),
        ErrorMessage = "CreatedTo must be greater than or equal to CreatedFrom."
    )]
    public DateTime? CreatedTo { get; init; }

    [CaseInsensitiveAllowedValues(
        ProductReviewSortFields.RatingToken,
        ProductReviewSortFields.CreatedAtToken,
        ErrorMessage = "SortBy must be one of: Rating, CreatedAt."
    )]
    public string? SortBy { get; init; }

    [SortDirection]
    public string? SortDirection { get; init; }
}
