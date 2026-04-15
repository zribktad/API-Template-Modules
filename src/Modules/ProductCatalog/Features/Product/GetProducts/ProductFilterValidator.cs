using FluentValidation;

namespace ProductCatalog.Features.Product.GetProducts;

public sealed class ProductFilterValidator : AbstractValidator<ProductFilter>
{
    public ProductFilterValidator()
    {
        RuleFor(x => x.MaxPrice)
            .GreaterThanOrEqualTo(x => x.MinPrice!.Value)
            .WithMessage("MaxPrice must be greater than or equal to MinPrice.")
            .When(x => x.MinPrice.HasValue && x.MaxPrice.HasValue);

        RuleFor(x => x.CreatedTo)
            .GreaterThanOrEqualTo(x => x.CreatedFrom!.Value)
            .WithMessage("CreatedTo must be greater than or equal to CreatedFrom.")
            .When(x => x.CreatedFrom.HasValue && x.CreatedTo.HasValue);
    }
}
