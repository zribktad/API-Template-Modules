namespace ProductCatalog.Features.Product.UpdateProducts;

/// <summary>
///     FluentValidation validator for <see cref="UpdateProductRequest" />: cross-field rules only; attribute
///     constraints are applied by ASP.NET Core on the bound body.
/// </summary>
public sealed class UpdateProductRequestValidator
    : ProductRequestValidatorBase<UpdateProductRequest>;
