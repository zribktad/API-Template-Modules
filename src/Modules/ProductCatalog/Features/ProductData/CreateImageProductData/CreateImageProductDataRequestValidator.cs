namespace ProductCatalog.Features.ProductData.CreateImageProductData;

/// <summary>
///     FluentValidation validator for <see cref="CreateImageProductDataRequest" />, delegating to data-annotation-based
///     validation rules.
/// </summary>
public sealed class CreateImageProductDataRequestValidator
    : DataAnnotationsValidator<CreateImageProductDataRequest>;
