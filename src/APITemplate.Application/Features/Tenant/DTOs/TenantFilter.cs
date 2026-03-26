using APITemplate.Application.Common.Contracts;
using APITemplate.Application.Common.DTOs;

namespace APITemplate.Application.Features.Tenant.DTOs;

/// <summary>
/// Pagination and filtering parameters for querying tenants, including optional full-text search and sorting.
/// </summary>
public sealed record TenantFilter(
    string? Query = null,
    string? SortBy = null,
    string? SortDirection = null,
    int PageNumber = 1,
    int PageSize = PaginationFilter.DefaultPageSize
) : PaginationFilter(PageNumber, PageSize), ISortableFilter;
