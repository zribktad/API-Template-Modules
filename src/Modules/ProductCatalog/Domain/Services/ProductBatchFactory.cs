using ErrorOr;
using ProductCatalog.Features.Product.CreateProducts;
using ProductCatalog.ValueObjects;
using ProductEntity = ProductCatalog.Entities.Product;

namespace ProductCatalog.Domain.Services;

/// <summary>
///     Default <see cref="IProductBatchFactory" />. Aggregates per-item failures into a single
///     <see cref="BatchFailureContext{T}" /> so each index can collect errors from multiple validation layers
///     (fluent rule, reference check, price value-object) before being rejected.
/// </summary>
internal sealed class ProductBatchFactory(
    IProductReferenceValidator referenceValidator,
    IBatchRule<CreateProductRequest> itemValidationRule
) : IProductBatchFactory
{
    public async Task<ProductBatchCreateResult> CreateAsync(
        IReadOnlyList<CreateProductRequest> items,
        CancellationToken ct
    )
    {
        BatchFailureContext<CreateProductRequest> context = new(items);

        await context.ApplyRulesAsync(ct, itemValidationRule);

        context.AddFailures(
            await referenceValidator.CheckReferencesAsync(items, context.FailedIndices, ct)
        );

        for (int i = 0; i < items.Count; i++)
        {
            if (context.IsFailed(i))
                continue;

            ErrorOr<Price> priceResult = Price.Create(items[i].Price);
            if (priceResult.IsError)
                context.AddFailure(i, null, priceResult.FirstError.Description);
        }

        if (context.HasFailures)
            return new ProductBatchCreateResult(null, context.ToFailureResponse());

        List<ProductEntity> entities = items
            .Select(item =>
                ProductEntity.Create(
                    item.Name,
                    item.Description,
                    Price.FromPersistence(item.Price),
                    item.CategoryId,
                    item.ProductDataIds
                )
            )
            .ToList();

        return new ProductBatchCreateResult(entities, null);
    }
}
