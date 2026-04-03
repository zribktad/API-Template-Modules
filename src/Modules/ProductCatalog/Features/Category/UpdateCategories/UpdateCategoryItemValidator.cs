using SharedKernel.Application.Validation;

namespace ProductCatalog.Features.Category.UpdateCategories;

/// <summary>
/// FluentValidation validator for <see cref="UpdateCategoryItem"/> that enforces data-annotation constraints.
/// </summary>
public sealed class UpdateCategoryItemValidator : DataAnnotationsValidator<UpdateCategoryItem>;
