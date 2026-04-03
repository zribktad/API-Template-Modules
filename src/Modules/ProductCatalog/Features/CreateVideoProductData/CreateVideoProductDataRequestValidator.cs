using SharedKernel.Application.Validation;

namespace ProductCatalog.Features.CreateVideoProductData;

/// <summary>
/// FluentValidation validator for <see cref="CreateVideoProductDataRequest"/>, delegating to data-annotation-based validation rules.
/// </summary>
public sealed class CreateVideoProductDataRequestValidator
    : DataAnnotationsValidator<CreateVideoProductDataRequest>;
