using APITemplate.Application.Common.Validation;

namespace APITemplate.Application.Features.Category.Validation;

/// <summary>
/// FluentValidation validator for <see cref="CreateCategoryRequest"/> that enforces data-annotation constraints.
/// </summary>
public sealed class CreateCategoryRequestValidator
    : DataAnnotationsValidator<CreateCategoryRequest>;
