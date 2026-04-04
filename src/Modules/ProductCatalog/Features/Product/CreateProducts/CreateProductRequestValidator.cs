namespace ProductCatalog.Features.Product.CreateProducts;

/// <summary>
/// FluentValidation validator for <see cref="CreateProductRequest"/>, inheriting all rules from <see cref="ProductRequestValidatorBase{T}"/>.
/// </summary>
public sealed class CreateProductRequestValidator
    : ProductRequestValidatorBase<CreateProductRequest>;
