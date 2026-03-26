namespace APITemplate.Application.Features.Product.Validation;

/// <summary>
/// FluentValidation validator for <see cref="CreateProductRequest"/>, inheriting all rules from <see cref="ProductRequestValidatorBase{T}"/>.
/// </summary>
public sealed class CreateProductRequestValidator
    : ProductRequestValidatorBase<CreateProductRequest>;
