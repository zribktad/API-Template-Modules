using System.ComponentModel.DataAnnotations;
using Identity.Directory.Features.Tenant;
using SharedKernel.Application.Validation;

namespace Identity.Directory.Features.Tenant.DTOs;

/// <summary>
///     Pagination and filtering parameters for querying tenants, including optional full-text search and sorting.
/// </summary>
public sealed record TenantFilter(
    string? Query = null,
    [CaseInsensitiveAllowedValues(
        TenantSortFields.CodeToken,
        TenantSortFields.NameToken,
        TenantSortFields.CreatedAtToken,
        ErrorMessage = "SortBy must be one of: Code, Name, CreatedAt."
    )]
    string? SortBy = null,
    [SortDirection]
    string? SortDirection = null,
    int PageNumber = 1,
    int PageSize = PaginationFilter.DefaultPageSize
) : PaginationFilter(PageNumber, PageSize), ISortableFilter;
