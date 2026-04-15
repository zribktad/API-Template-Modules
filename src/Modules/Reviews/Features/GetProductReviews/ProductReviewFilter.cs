using System.ComponentModel.DataAnnotations;
using SharedKernel.Application.Validation;

namespace Reviews.Features;

/// <summary>
///     Filter parameters for querying product reviews, supporting filtering by product, user, rating range, date range,
///     sorting, and pagination.
/// </summary>
public sealed record ProductReviewFilter(
    Guid? ProductId = null,
    Guid? UserId = null,
    [Range(1, 5, ErrorMessage = "MinRating must be between 1 and 5.")]
    int? MinRating = null,
    [Range(1, 5, ErrorMessage = "MaxRating must be between 1 and 5.")]
    [GreaterThanOrEqualToProperty(nameof(MinRating), ErrorMessage = "MaxRating must be greater than or equal to MinRating.")]
    int? MaxRating = null,
    DateTime? CreatedFrom = null,
    [GreaterThanOrEqualToProperty(nameof(CreatedFrom), ErrorMessage = "CreatedTo must be greater than or equal to CreatedFrom.")]
    DateTime? CreatedTo = null,
    [AllowedValues("rating", "createdAt", ErrorMessage = "SortBy must be one of: rating, createdAt.")]
    string? SortBy = null,
    [SortDirection]
    string? SortDirection = null,
    int PageNumber = 1,
    int PageSize = PaginationFilter.DefaultPageSize
) : PaginationFilter(PageNumber, PageSize), IDateRangeFilter, ISortableFilter;
