using System.ComponentModel.DataAnnotations;
using SharedKernel.Application.Validation;

namespace Identity.Directory.Features.Tenant.DTOs;

/// <summary>
///     Pagination and filtering parameters for querying tenants, including optional full-text search and sorting.
/// </summary>
public sealed record TenantFilter(
    string? Query = null,
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
                TenantSortFields.Map.AllowedNames
            )
        )
        {
            yield return validationResult;
        }
    }
}
