using ErrorOr;
using ProductCatalog.Features.Product.CreateProducts;
using ProductCatalog.ValueObjects;
using ProductEntity = ProductCatalog.Entities.Product;

namespace ProductCatalog.Domain.Services;

/// <summary>
///     Default <see cref="IProductBatchFactory" />. Delegates the validation pipeline to
///     <see cref="IProductBatchValidator{T}" /> and uses the validated <see cref="Price" /> values to construct
///     not-yet-persisted <see cref="ProductEntity" /> aggregates.
/// </summary>
internal sealed class ProductBatchFactory(IProductBatchValidator<CreateProductRequest> validator)
    : IProductBatchFactory
{
    public async Task<ErrorOr<IReadOnlyList<ProductEntity>>> CreateAsync(
        IReadOnlyList<CreateProductRequest> items,
        CancellationToken ct
    )
    {
        ErrorOr<IReadOnlyList<Price>> validation = await validator.ValidateAsync(items, ct);
        if (validation.IsError)
            return validation.Errors;

        IReadOnlyList<Price> prices = validation.Value;
        List<ProductEntity> entities = new(items.Count);
        for (int i = 0; i < items.Count; i++)
        {
            CreateProductRequest item = items[i];
            entities.Add(
                ProductEntity.Create(
                    item.Name,
                    item.Description,
                    prices[i],
                    item.CategoryId,
                    item.ProductDataIds
                )
            );
        }

        return entities;
    }
}
