using APITemplate.Application.Common.Validation;
using FluentValidation;

namespace APITemplate.Application.Features.Product.Validation;

/// <summary>
/// FluentValidation validator for <see cref="ProductFilter"/>; composes pagination, date-range, sortable-field, and price-range rules including cross-field MinPrice/MaxPrice consistency.
/// </summary>
public sealed class ProductFilterValidator : AbstractValidator<ProductFilter>
{
    public ProductFilterValidator()
    {
        Include(new PaginationFilterValidator());
        Include(new DateRangeFilterValidator<ProductFilter>());
        Include(new SortableFilterValidator<ProductFilter>(ProductSortFields.Map.AllowedNames));

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

        RuleForEach(x => x.CategoryIds)
            .NotEqual(Guid.Empty)
            .WithMessage("CategoryIds cannot contain an empty value.");
    }
}
