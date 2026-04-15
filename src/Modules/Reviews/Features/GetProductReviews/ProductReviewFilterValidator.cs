using FluentValidation;
using Reviews.Domain;

namespace Reviews.Features;

public sealed class ProductReviewFilterValidator : AbstractValidator<ProductReviewFilter>
{
    public ProductReviewFilterValidator()
    {
        RuleFor(x => x.PageNumber)
            .GreaterThanOrEqualTo(1)
            .WithMessage("PageNumber must be greater than or equal to 1.");

        RuleFor(x => x.PageSize)
            .InclusiveBetween(1, PaginationFilter.MaxPageSize)
            .WithMessage("PageSize must be between 1 and 100.");

        RuleFor(x => x.SortBy)
            .Must(sortBy =>
                sortBy is null
                || ProductReviewSortFields.Map.AllowedNames.Any(name =>
                    name.Equals(sortBy, StringComparison.OrdinalIgnoreCase)
                )
            )
            .WithMessage($"SortBy must be one of: {string.Join(", ", ProductReviewSortFields.Map.AllowedNames)}.");

        RuleFor(x => x.SortDirection)
            .Must(direction =>
                direction is null
                || direction.Equals("asc", StringComparison.OrdinalIgnoreCase)
                || direction.Equals("desc", StringComparison.OrdinalIgnoreCase)
            )
            .WithMessage("SortDirection must be one of: asc, desc.");

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

        RuleFor(x => x.CreatedTo)
            .GreaterThanOrEqualTo(x => x.CreatedFrom!.Value)
            .WithMessage("CreatedTo must be greater than or equal to CreatedFrom.")
            .When(x => x.CreatedFrom.HasValue && x.CreatedTo.HasValue);
    }
}
