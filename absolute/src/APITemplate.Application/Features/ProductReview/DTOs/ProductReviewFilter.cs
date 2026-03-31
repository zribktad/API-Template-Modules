using APITemplate.Application.Common.Contracts;
using APITemplate.Application.Common.DTOs;

namespace APITemplate.Application.Features.ProductReview.DTOs;

/// <summary>
/// Filter parameters for querying product reviews, supporting filtering by product, user, rating range, date range, sorting, and pagination.
/// </summary>
public sealed record ProductReviewFilter(
    Guid? ProductId = null,
    Guid? UserId = null,
    int? MinRating = null,
    int? MaxRating = null,
    DateTime? CreatedFrom = null,
    DateTime? CreatedTo = null,
    string? SortBy = null,
    string? SortDirection = null,
    int PageNumber = 1,
    int PageSize = PaginationFilter.DefaultPageSize
) : PaginationFilter(PageNumber, PageSize), IDateRangeFilter, ISortableFilter;
