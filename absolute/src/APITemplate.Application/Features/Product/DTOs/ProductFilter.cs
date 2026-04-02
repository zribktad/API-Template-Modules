using APITemplate.Application.Common.Contracts;
using APITemplate.Application.Common.DTOs;

namespace APITemplate.Application.Features.Product.DTOs;

/// <summary>
/// Encapsulates all criteria available for querying and paging the product list, including text search, price range, date range, category filtering, and sorting.
/// </summary>
public sealed record ProductFilter(
    string? Name = null,
    string? Description = null,
    decimal? MinPrice = null,
    decimal? MaxPrice = null,
    DateTime? CreatedFrom = null,
    DateTime? CreatedTo = null,
    string? SortBy = null,
    string? SortDirection = null,
    int PageNumber = 1,
    int PageSize = PaginationFilter.DefaultPageSize,
    string? Query = null,
    IReadOnlyCollection<Guid>? CategoryIds = null
) : PaginationFilter(PageNumber, PageSize), IDateRangeFilter, ISortableFilter;
