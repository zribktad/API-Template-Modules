namespace ProductCatalog.Features.Product.UpdateProducts;

/// <summary>
///     FluentValidation validator for batch <see cref="UpdateProductItem" /> rows: cross-field rules only; attribute
///     constraints are applied by ASP.NET Core on the request body.
/// </summary>
public sealed class UpdateProductItemValidator : ProductRequestValidatorBase<UpdateProductItem>;
