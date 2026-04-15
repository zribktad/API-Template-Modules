using FluentValidation;
using ProductCatalog.Features.Category.Shared;

namespace ProductCatalog.Features.Category.GetCategories;

public sealed class CategoryFilterValidator : AbstractValidator<CategoryFilter>
{
    public CategoryFilterValidator()
    {
        RuleFor(x => x.PageNumber)
            .GreaterThanOrEqualTo(1)
            .WithMessage("PageNumber must be greater than or equal to 1.");

        RuleFor(x => x.PageSize)
            .InclusiveBetween(1, PaginationFilter.MaxPageSize)
            .WithMessage("PageSize must be between 1 and 100.");

        RuleFor(x => x.SortBy)
            .Must(sortBy =>
                sortBy is null
                || CategorySortFields.Map.AllowedNames.Any(name =>
                    name.Equals(sortBy, StringComparison.OrdinalIgnoreCase)
                )
            )
            .WithMessage($"SortBy must be one of: {string.Join(", ", CategorySortFields.Map.AllowedNames)}.");

        RuleFor(x => x.SortDirection)
            .Must(direction =>
                direction is null
                || direction.Equals("asc", StringComparison.OrdinalIgnoreCase)
                || direction.Equals("desc", StringComparison.OrdinalIgnoreCase)
            )
            .WithMessage("SortDirection must be one of: asc, desc.");
    }
}
