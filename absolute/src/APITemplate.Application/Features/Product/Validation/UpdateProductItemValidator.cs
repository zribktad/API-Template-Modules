namespace APITemplate.Application.Features.Product.Validation;

/// <summary>
/// FluentValidation validator for <see cref="UpdateProductItem"/>, reusing the shared
/// product validation rules including the description-required-above-price-threshold rule.
/// </summary>
public sealed class UpdateProductItemValidator : ProductRequestValidatorBase<UpdateProductItem>;
