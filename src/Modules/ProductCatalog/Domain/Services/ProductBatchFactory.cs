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

        Price[] prices = new Price[items.Count];
        for (int i = 0; i < items.Count; i++)
        {
            if (context.IsFailed(i))
                continue;

            ErrorOr<Price> priceResult = Price.Create(items[i].Price);
            if (priceResult.IsError)
            {
                context.AddFailure(i, null, priceResult.FirstError.Description);
                continue;
            }

            prices[i] = priceResult.Value;
        }

        if (context.HasFailures)
            return new ProductBatchCreateResult(null, context.ToFailureResponse());

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

        return new ProductBatchCreateResult(entities, null);
    }
}
