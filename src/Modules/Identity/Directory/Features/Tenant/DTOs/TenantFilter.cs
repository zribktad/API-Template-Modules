using System.ComponentModel.DataAnnotations;
using SharedKernel.Application.Validation;

namespace Identity.Directory.Features.Tenant.DTOs;

/// <summary>
///     Pagination and filtering parameters for querying tenants, including optional full-text search and sorting.
/// </summary>
public sealed record TenantFilter(
    string? Query = null,
    [AllowedValues("code", "name", "createdAt", ErrorMessage = "SortBy must be one of: code, name, createdAt.")]
    string? SortBy = null,
    [SortDirection]
    string? SortDirection = null,
    int PageNumber = 1,
    int PageSize = PaginationFilter.DefaultPageSize
) : PaginationFilter(PageNumber, PageSize), ISortableFilter;
