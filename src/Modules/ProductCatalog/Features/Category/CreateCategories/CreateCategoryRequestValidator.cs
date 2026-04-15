using FluentValidation;

namespace ProductCatalog.Features.Category.CreateCategories;

/// <summary>
///     FluentValidation validator for <see cref="CreateCategoryRequest" />.
/// </summary>
public sealed class CreateCategoryRequestValidator : AbstractValidator<CreateCategoryRequest>
{
    public CreateCategoryRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .WithMessage("Category name is required.")
            .MaximumLength(200)
            .WithMessage("Category name must not exceed 200 characters.");
    }
}
