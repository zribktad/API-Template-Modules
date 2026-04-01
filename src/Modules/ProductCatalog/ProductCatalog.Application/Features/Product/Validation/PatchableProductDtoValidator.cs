using FluentValidation;
using ProductCatalog.Application.Features.Product.DTOs;
using ProductCatalog.Application.Features.Product.Validation;
using SharedKernel.Application.Validation;

namespace ProductCatalog.Application.Features.Product.Validation;

/// <summary>
/// FluentValidation validator for the post-patch <see cref="PatchableProductDto"/> state;
/// applies data-annotation constraints and the shared description-required-above-price-threshold rule.
/// </summary>
public sealed class PatchableProductDtoValidator : DataAnnotationsValidator<PatchableProductDto>
{
    public PatchableProductDtoValidator()
    {
        RuleFor(x => x.Description).RequiredAbovePriceThreshold(x => x.Price);
    }
}
