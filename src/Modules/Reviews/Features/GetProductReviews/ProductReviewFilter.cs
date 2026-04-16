using System.ComponentModel.DataAnnotations;

namespace Reviews.Features;

/// <summary>
///     Filter parameters for querying product reviews, supporting filtering by product, user, rating range, date range,
///     sorting, and pagination.
/// </summary>
/// <remarks>
///     Query-string DTO for a Wolverine HTTP endpoint — must stay a primary-constructor record. Wolverine's
///     <c>QueryStringBindingFrame</c> either binds through constructor parameters or generates post-<c>new</c>
///     property assignments, so <c>{ get; init; }</c> would fail codegen. Validation attributes use the
///     <c>[property: ...]</c> target so they land on the generated properties where Wolverine's HTTP validation
///     policy (<c>Validator.TryValidateObject</c>) can see them. See <c>docs/validation.md</c> — Record DTO convention.
/// </remarks>
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
