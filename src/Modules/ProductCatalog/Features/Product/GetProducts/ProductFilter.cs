using System.ComponentModel.DataAnnotations;
using SharedKernel.Application.Validation;

namespace ProductCatalog.Features.Product.GetProducts;

/// <summary>
///     Encapsulates all criteria available for querying and paging the product list, including text search, price range,
///     date range, category filtering, and sorting.
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
) : PaginationFilter(PageNumber, PageSize), IDateRangeFilter, ISortableFilter, IValidatableObject
{
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        foreach (
            ValidationResult validationResult in BoundaryValidation.ValidateSort(
                SortBy,
                SortDirection,
                ProductSortFields.Map.AllowedNames
            )
        )
        {
            yield return validationResult;
        }

        ValidationResult? dateRangeResult = BoundaryValidation.ValidateDateRange(
            CreatedFrom,
            CreatedTo
        );
        if (dateRangeResult is not null)
            yield return dateRangeResult;

        if (MinPrice.HasValue && MinPrice.Value < 0)
        {
            yield return new ValidationResult(
                "MinPrice must be greater than or equal to zero.",
                [nameof(MinPrice)]
            );
        }

        if (MaxPrice.HasValue && MaxPrice.Value < 0)
        {
            yield return new ValidationResult(
                "MaxPrice must be greater than or equal to zero.",
                [nameof(MaxPrice)]
            );
        }

        if (MinPrice.HasValue && MaxPrice.HasValue && MaxPrice.Value < MinPrice.Value)
        {
            yield return new ValidationResult(
                "MaxPrice must be greater than or equal to MinPrice.",
                [nameof(MaxPrice)]
            );
        }

        if (CategoryIds is null)
            yield break;

        int index = 0;
        foreach (Guid categoryId in CategoryIds)
        {
            if (categoryId == Guid.Empty)
            {
                yield return new ValidationResult(
                    "CategoryIds cannot contain an empty value.",
                    [$"{nameof(CategoryIds)}[{index}]"]
                );
            }

            index++;
        }
    }
}
