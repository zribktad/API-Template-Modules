using APITemplate.Application.Common.Contracts;
using APITemplate.Application.Common.DTOs;
using APITemplate.Domain.Enums;

namespace APITemplate.Application.Features.User.DTOs;

/// <summary>
/// Pagination and filtering parameters for querying users, with optional username, email, active-status, role, and sort fields.
/// </summary>
public sealed record UserFilter(
    string? Username = null,
    string? Email = null,
    bool? IsActive = null,
    UserRole? Role = null,
    string? SortBy = null,
    string? SortDirection = null,
    int PageNumber = 1,
    int PageSize = PaginationFilter.DefaultPageSize
) : PaginationFilter(PageNumber, PageSize), ISortableFilter;
