using SharedKernel.Application.Contracts;
using SharedKernel.Application.DTOs;

namespace ProductCatalog.Features.GetCategories;

/// <summary>
/// Filter parameters for querying categories, supporting full-text search, sorting, and pagination.
/// </summary>
public sealed record CategoryFilter(
    string? Query = null,
    string? SortBy = null,
    string? SortDirection = null,
    int PageNumber = 1,
    int PageSize = PaginationFilter.DefaultPageSize
) : PaginationFilter(PageNumber, PageSize), ISortableFilter;
