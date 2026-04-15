using FluentValidation;
using ProductCatalog.Features.Product.Shared;

namespace ProductCatalog.Features.Product.GetProducts;

public sealed class ProductFilterValidator : AbstractValidator<ProductFilter>
{
    public ProductFilterValidator()
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
                || ProductSortFields.Map.AllowedNames.Any(name =>
                    name.Equals(sortBy, StringComparison.OrdinalIgnoreCase)
                )
            )
            .WithMessage($"SortBy must be one of: {string.Join(", ", ProductSortFields.Map.AllowedNames)}.");

        RuleFor(x => x.SortDirection)
            .Must(direction =>
                direction is null
                || direction.Equals("asc", StringComparison.OrdinalIgnoreCase)
                || direction.Equals("desc", StringComparison.OrdinalIgnoreCase)
            )
            .WithMessage("SortDirection must be one of: asc, desc.");

        RuleFor(x => x.MinPrice)
            .GreaterThanOrEqualTo(0)
            .WithMessage("MinPrice must be greater than or equal to zero.")
            .When(x => x.MinPrice.HasValue);

        RuleFor(x => x.MaxPrice)
            .GreaterThanOrEqualTo(0)
            .WithMessage("MaxPrice must be greater than or equal to zero.")
            .When(x => x.MaxPrice.HasValue);

        RuleFor(x => x.MaxPrice)
            .GreaterThanOrEqualTo(x => x.MinPrice!.Value)
            .WithMessage("MaxPrice must be greater than or equal to MinPrice.")
            .When(x => x.MinPrice.HasValue && x.MaxPrice.HasValue);

        RuleFor(x => x.CreatedTo)
            .GreaterThanOrEqualTo(x => x.CreatedFrom!.Value)
            .WithMessage("CreatedTo must be greater than or equal to CreatedFrom.")
            .When(x => x.CreatedFrom.HasValue && x.CreatedTo.HasValue);

        RuleForEach(x => x.CategoryIds)
            .NotEqual(Guid.Empty)
            .WithMessage("CategoryIds cannot contain an empty value.");
    }
}
