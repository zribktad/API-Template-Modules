using System.ComponentModel.DataAnnotations;
using SharedKernel.Application.Validation;

namespace Identity.Directory.Features.User;

/// <summary>
///     Pagination and filtering parameters for querying users, with optional username, email, active-status, role, and
///     sort fields.
/// </summary>
public sealed record UserFilter(
    string? Username = null,
    string? Email = null,
    bool? IsActive = null,
    Guid? RoleId = null,
    ProvisioningStatus? ProvisioningStatus = null,
    [AllowedValues("username", "email", "createdAt", ErrorMessage = "SortBy must be one of: username, email, createdAt.")]
    string? SortBy = null,
    [SortDirection]
    string? SortDirection = null,
    int PageNumber = 1,
    int PageSize = PaginationFilter.DefaultPageSize
) : PaginationFilter(PageNumber, PageSize), ISortableFilter;
