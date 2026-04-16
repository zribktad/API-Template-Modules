using System.ComponentModel.DataAnnotations;

namespace Reviews.Features;

/// <summary>
///     Filter parameters for querying product reviews, supporting filtering by product, user, rating range, date range,
///     sorting, and pagination.
/// </summary>
/// <remarks>
///     Wolverine HTTP filter DTO. Stays a primary-constructor record (query-string binding uses ctor params) and
///     uses <c>[property: ...]</c> targets so <c>Validator.TryValidateObject</c> sees the attributes. Pagination
///     fields are inlined instead of inherited from <c>PaginationFilter</c>: inheriting would shadow the base
///     property and forbid a <c>[property:]</c> target on the derived ctor param (CS0657). Composing pagination
///     as a nested <c>PaginationFilter</c> ctor param also fails — Wolverine's query-string binder only handles
///     primitive/string types. See <c>docs/validation.md</c> — Record DTO convention.
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
    [property: Range(
        1,
        int.MaxValue,
        ErrorMessage = "PageNumber must be greater than or equal to 1."
    )]
        int PageNumber = 1,
    [property: Range(1, 100, ErrorMessage = "PageSize must be between 1 and 100.")]
        int PageSize = PaginationFilter.DefaultPageSize
) : IDateRangeFilter, ISortableFilter;
