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
    string? SortBy = null,
    string? SortDirection = null,
    int PageNumber = 1,
    int PageSize = PaginationFilter.DefaultPageSize
) : PaginationFilter(PageNumber, PageSize), ISortableFilter, IValidatableObject
{
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        foreach (
            ValidationResult validationResult in BoundaryValidation.ValidateSort(
                SortBy,
                SortDirection,
                UserSortFields.Map.AllowedNames
            )
        )
        {
            yield return validationResult;
        }
    }
}
