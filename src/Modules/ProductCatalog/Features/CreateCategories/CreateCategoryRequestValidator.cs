using SharedKernel.Application.Validation;

namespace ProductCatalog.Features.CreateCategories;

/// <summary>
/// FluentValidation validator for <see cref="CreateCategoryRequest"/> that enforces data-annotation constraints.
/// </summary>
public sealed class CreateCategoryRequestValidator
    : DataAnnotationsValidator<CreateCategoryRequest>;
