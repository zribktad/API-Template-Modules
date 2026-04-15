using System.ComponentModel.DataAnnotations;
using SharedKernel.Application.Validation;

namespace ProductCatalog.Features.Category.GetCategories;

/// <summary>
///     Filter parameters for querying categories, supporting full-text search, sorting, and pagination.
/// </summary>
public sealed record CategoryFilter(
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
                CategorySortFields.Map.AllowedNames
            )
        )
        {
            yield return validationResult;
        }
    }
}
