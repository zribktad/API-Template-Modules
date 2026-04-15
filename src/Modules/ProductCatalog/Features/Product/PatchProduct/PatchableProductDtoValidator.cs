using FluentValidation;

namespace ProductCatalog.Features.Product.PatchProduct;

/// <summary>
///     FluentValidation validator for the post-patch <see cref="PatchableProductDto" /> state;
///     applies the common field rules and the shared description-required-above-price-threshold rule.
/// </summary>
public sealed class PatchableProductDtoValidator : AbstractValidator<PatchableProductDto>
{
    public PatchableProductDtoValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .WithMessage("Name is required.")
            .MaximumLength(200);

        RuleFor(x => x.Description).MaximumLength(1000);

        RuleFor(x => x.Price)
            .GreaterThanOrEqualTo(0)
            .WithMessage("Price must be non-negative.");

        RuleFor(x => x.Description).RequiredAbovePriceThreshold(x => x.Price);
    }
}
