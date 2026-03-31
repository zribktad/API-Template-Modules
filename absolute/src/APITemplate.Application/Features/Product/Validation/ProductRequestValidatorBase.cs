using APITemplate.Application.Common.Validation;
using FluentValidation;

namespace APITemplate.Application.Features.Product.Validation;

/// <summary>
/// Shared FluentValidation extension methods and constants for product-related validation rules.
/// </summary>
public static class ProductValidationRules
{
    public const decimal DescriptionRequiredPriceThreshold = 1000;
    public const string DescriptionRequiredMessage =
        "Description is required for products priced above 1000.";

    /// <summary>
    /// Adds a rule that makes the string property non-empty when the product price exceeds <see cref="DescriptionRequiredPriceThreshold"/>.
    /// </summary>
    public static IRuleBuilderOptions<T, string?> RequiredAbovePriceThreshold<T>(
        this IRuleBuilder<T, string?> ruleBuilder,
        Func<T, decimal> priceAccessor
    ) =>
        ruleBuilder
            .NotEmpty()
            .WithMessage(DescriptionRequiredMessage)
            .When(x => priceAccessor(x) > DescriptionRequiredPriceThreshold);
}

/// <summary>
/// Abstract base validator for create/update product requests; inherits data-annotation validation and adds the shared description-required-above-price-threshold rule.
/// </summary>
public abstract class ProductRequestValidatorBase<T> : DataAnnotationsValidator<T>
    where T : class, IProductRequest
{
    protected ProductRequestValidatorBase()
    {
        RuleFor(x => x.Description).RequiredAbovePriceThreshold(x => x.Price);
    }
}
