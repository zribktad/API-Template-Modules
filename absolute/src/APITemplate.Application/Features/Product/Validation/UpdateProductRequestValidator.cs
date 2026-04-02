namespace APITemplate.Application.Features.Product.Validation;

/// <summary>
/// FluentValidation validator for <see cref="UpdateProductRequest"/>, inheriting all rules from <see cref="ProductRequestValidatorBase{T}"/>.
/// </summary>
public sealed class UpdateProductRequestValidator
    : ProductRequestValidatorBase<UpdateProductRequest>;
