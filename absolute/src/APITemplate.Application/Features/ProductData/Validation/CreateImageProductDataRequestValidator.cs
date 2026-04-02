using APITemplate.Application.Common.Validation;

namespace APITemplate.Application.Features.ProductData.Validation;

/// <summary>
/// FluentValidation validator for <see cref="CreateImageProductDataRequest"/>, delegating to data-annotation-based validation rules.
/// </summary>
public sealed class CreateImageProductDataRequestValidator
    : DataAnnotationsValidator<CreateImageProductDataRequest>;
