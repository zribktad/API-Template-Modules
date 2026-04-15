using FluentValidation;

namespace Reviews.Features;

public sealed class ProductReviewFilterValidator : AbstractValidator<ProductReviewFilter>
{
    public ProductReviewFilterValidator()
    {
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
