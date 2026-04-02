using APITemplate.Application.Common.Validation;
using APITemplate.Application.Features.Examples.DTOs;
using APITemplate.Application.Features.Product.Validation;
using FluentValidation;

namespace APITemplate.Application.Features.Examples.Validation;

/// <summary>
/// FluentValidation validator for the post-patch <see cref="PatchableProductDto"/> state; applies data-annotation constraints and the shared description-required-above-price-threshold rule.
/// </summary>
public sealed class PatchableProductDtoValidator : DataAnnotationsValidator<PatchableProductDto>
{
    public PatchableProductDtoValidator()
    {
        RuleFor(x => x.Description).RequiredAbovePriceThreshold(x => x.Price);
    }
}
