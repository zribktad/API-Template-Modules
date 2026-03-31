using SharedKernel.Application.Validation;
using FluentValidation;

namespace ProductCatalog.Application.Features.Category.Validation;

/// <summary>
/// FluentValidation validator for <see cref="CategoryFilter"/>.
/// Composes pagination and sortable filter validation rules by inclusion.
/// </summary>
public sealed class CategoryFilterValidator : AbstractValidator<CategoryFilter>
{
    public CategoryFilterValidator()
    {
        Include(new PaginationFilterValidator());
        Include(new SortableFilterValidator<CategoryFilter>(CategorySortFields.Map.AllowedNames));
    }
}


