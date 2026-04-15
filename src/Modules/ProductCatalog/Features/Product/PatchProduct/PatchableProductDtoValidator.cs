using FluentValidation;

namespace ProductCatalog.Features.Product.PatchProduct;

/// <summary>
///     FluentValidation validator for the post-patch <see cref="PatchableProductDto" /> state; Data Annotations cover
///     field-level constraints and this validator keeps only the cross-field description rule.
/// </summary>
public sealed class PatchableProductDtoValidator : AbstractValidator<PatchableProductDto>
{
    public PatchableProductDtoValidator()
    {
        RuleFor(x => x.Description).RequiredAbovePriceThreshold(x => x.Price);
    }
}
