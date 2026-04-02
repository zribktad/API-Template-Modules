using APITemplate.Application.Common.Validation;

namespace APITemplate.Application.Features.Category.Validation;

/// <summary>
/// FluentValidation validator for <see cref="UpdateCategoryItem"/> that enforces data-annotation constraints.
/// </summary>
public sealed class UpdateCategoryItemValidator : DataAnnotationsValidator<UpdateCategoryItem>;
