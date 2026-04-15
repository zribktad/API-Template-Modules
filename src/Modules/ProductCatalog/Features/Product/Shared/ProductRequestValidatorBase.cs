using FluentValidation;

namespace ProductCatalog.Features.Product.Shared;

/// <summary>
///     Shared FluentValidation extension methods and constants for product-related validation rules.
/// </summary>
public static class ProductValidationRules
{
    public const decimal DescriptionRequiredPriceThreshold = 1000;

    public const string DescriptionRequiredMessage =
        "Description is required for products priced above 1000.";

    /// <summary>
    ///     Adds a rule that makes the string property non-empty when the product price exceeds
    ///     <see cref="DescriptionRequiredPriceThreshold" />.
    /// </summary>
    public static IRuleBuilderOptions<T, string?> RequiredAbovePriceThreshold<T>(
        this IRuleBuilder<T, string?> ruleBuilder,
        Func<T, decimal> priceAccessor
    )
    {
        return ruleBuilder
            .NotEmpty()
            .WithMessage(DescriptionRequiredMessage)
            .When(x => priceAccessor(x) > DescriptionRequiredPriceThreshold);
    }
}

/// <summary>
///     Abstract base validator for create/update product requests; applies the common field rules and adds the shared
///     description-required-above-price-threshold rule.
/// </summary>
public abstract class ProductRequestValidatorBase<T> : AbstractValidator<T>
    where T : class, IProductRequest
{
    protected ProductRequestValidatorBase()
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .WithMessage("Product name is required.")
            .MaximumLength(200)
            .WithMessage("Product name must not exceed 200 characters.");

        RuleFor(x => x.Price)
            .GreaterThanOrEqualTo(0)
            .WithMessage("Price must be non-negative.");

        RuleFor(x => x.Description).RequiredAbovePriceThreshold(x => x.Price);
    }
}
