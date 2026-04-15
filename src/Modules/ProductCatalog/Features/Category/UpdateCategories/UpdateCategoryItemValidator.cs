using FluentValidation;

namespace ProductCatalog.Features.Category.UpdateCategories;

/// <summary>
///     FluentValidation validator for <see cref="UpdateCategoryItem" />.
/// </summary>
public sealed class UpdateCategoryItemValidator : AbstractValidator<UpdateCategoryItem>
{
    public UpdateCategoryItemValidator()
    {
        RuleFor(x => x.Id)
            .NotEqual(Guid.Empty)
            .WithMessage("Category ID is required.");

        RuleFor(x => x.Name)
            .NotEmpty()
            .WithMessage("Category name is required.")
            .MaximumLength(200)
            .WithMessage("Category name must not exceed 200 characters.");
    }
}
