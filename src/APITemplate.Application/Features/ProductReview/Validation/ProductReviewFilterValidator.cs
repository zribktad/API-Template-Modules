using APITemplate.Application.Common.Validation;
using FluentValidation;

namespace APITemplate.Application.Features.ProductReview.Validation;

/// <summary>
/// FluentValidation validator for <see cref="ProductReviewFilter"/>.
/// Composes pagination, date-range, sortable, and rating-range validation rules.
/// </summary>
public sealed class ProductReviewFilterValidator : AbstractValidator<ProductReviewFilter>
{
    public ProductReviewFilterValidator()
    {
        Include(new PaginationFilterValidator());
        Include(new DateRangeFilterValidator<ProductReviewFilter>());
        Include(
            new SortableFilterValidator<ProductReviewFilter>(
                ProductReviewSortFields.Map.AllowedNames
            )
        );

        RuleFor(x => x.MinRating)
            .InclusiveBetween(1, 5)
            .WithMessage("MinRating must be between 1 and 5.")
            .When(x => x.MinRating.HasValue);

        RuleFor(x => x.MaxRating)
            .InclusiveBetween(1, 5)
            .WithMessage("MaxRating must be between 1 and 5.")
            .When(x => x.MaxRating.HasValue);

        RuleFor(x => x.MaxRating)
            .GreaterThanOrEqualTo(x => x.MinRating!.Value)
            .WithMessage("MaxRating must be greater than or equal to MinRating.")
            .When(x => x.MinRating.HasValue && x.MaxRating.HasValue);
    }
}
