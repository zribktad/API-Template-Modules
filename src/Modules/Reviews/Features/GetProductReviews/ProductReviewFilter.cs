using System.ComponentModel.DataAnnotations;
using SharedKernel.Application.Validation;

namespace Reviews.Features;

/// <summary>
///     Filter parameters for querying product reviews, supporting filtering by product, user, rating range, date range,
///     sorting, and pagination.
/// </summary>
public sealed record ProductReviewFilter(
    Guid? ProductId = null,
    Guid? UserId = null,
    int? MinRating = null,
    int? MaxRating = null,
    DateTime? CreatedFrom = null,
    DateTime? CreatedTo = null,
    string? SortBy = null,
    string? SortDirection = null,
    int PageNumber = 1,
    int PageSize = PaginationFilter.DefaultPageSize
) : PaginationFilter(PageNumber, PageSize), IDateRangeFilter, ISortableFilter, IValidatableObject
{
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        foreach (
            ValidationResult validationResult in BoundaryValidation.ValidateSort(
                SortBy,
                SortDirection,
                ProductReviewSortFields.Map.AllowedNames
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

        if (MinRating.HasValue && (MinRating.Value < 1 || MinRating.Value > 5))
        {
            yield return new ValidationResult(
                "MinRating must be between 1 and 5.",
                [nameof(MinRating)]
            );
        }

        if (MaxRating.HasValue && (MaxRating.Value < 1 || MaxRating.Value > 5))
        {
            yield return new ValidationResult(
                "MaxRating must be between 1 and 5.",
                [nameof(MaxRating)]
            );
        }

        if (MinRating.HasValue && MaxRating.HasValue && MaxRating.Value < MinRating.Value)
        {
            yield return new ValidationResult(
                "MaxRating must be greater than or equal to MinRating.",
                [nameof(MaxRating)]
            );
        }
    }
}
