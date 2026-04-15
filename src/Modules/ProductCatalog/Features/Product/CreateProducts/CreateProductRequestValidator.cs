namespace ProductCatalog.Features.Product.CreateProducts;

/// <summary>
///     FluentValidation validator for <see cref="CreateProductRequest" /> batch items: cross-field rules only;
///     attribute constraints are applied by ASP.NET Core before the command runs.
/// </summary>
public sealed class CreateProductRequestValidator
    : ProductRequestValidatorBase<CreateProductRequest>;
