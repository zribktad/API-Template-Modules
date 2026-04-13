using FluentValidation;

namespace ProductCatalog.Features.Category.CreateCategories;

/// <summary>
///     FluentValidation hook for batch item rules. <see cref="CreateCategoryRequest" /> is validated via Data
///     Annotations at the API boundary; this type has no extra rules so batch does not re-run attribute validation.
/// </summary>
public sealed class CreateCategoryRequestValidator : AbstractValidator<CreateCategoryRequest>;
