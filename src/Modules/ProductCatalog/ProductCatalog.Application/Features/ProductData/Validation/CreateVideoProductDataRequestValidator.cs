using SharedKernel.Application.Validation;

namespace ProductCatalog.Application.Features.ProductData.Validation;

/// <summary>
/// FluentValidation validator for <see cref="CreateVideoProductDataRequest"/>, delegating to data-annotation-based validation rules.
/// </summary>
public sealed class CreateVideoProductDataRequestValidator
    : DataAnnotationsValidator<CreateVideoProductDataRequest>;


