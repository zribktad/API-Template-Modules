using ErrorOr;
using ProductCatalog;
using ProductCatalog.Entities;
using ProductCatalog.ValueObjects;
using SharedKernel.Application.Batch;
using SharedKernel.Application.Batch.Rules;
using SharedKernel.Contracts.Events;
using Wolverine;
using ProductEntity = ProductCatalog.Entities.Product;
using ProductRepositoryContract = ProductCatalog.Interfaces.IProductRepository;

namespace ProductCatalog.Features.Product.UpdateProducts;

/// <summary>Updates multiple products in a single batch operation.</summary>
public sealed record UpdateProductsCommand(UpdateProductsRequest Request);

/// <summary>Handles <see cref="UpdateProductsCommand"/> by validating all items, loading products in bulk, and updating in a single transaction.</summary>
public sealed class UpdateProductsCommandHandler
{
    /// <summary>
    /// Wolverine compound-handler load step: validates and loads products, short-circuiting the
    /// handler pipeline with a failure response when any validation rule fails.
    /// </summary>
    public static async Task<(
        HandlerContinuation,
        EntityLookup<ProductEntity>?,
        OutgoingMessages
    )> LoadAsync(
        UpdateProductsCommand command,
        ProductRepositoryContract repository,
        ICategoryRepository categoryRepository,
        IProductDataRepository productDataRepository,
        IBatchRule<UpdateProductItem> itemValidationRule,
        CancellationToken ct
    )
    {
        IReadOnlyList<UpdateProductItem> items = command.Request.Items;
        BatchFailureContext<UpdateProductItem> context = new(items);

        await context.ApplyRulesAsync(ct, itemValidationRule);

        HashSet<Guid> requestedIds = items
            .Where((_, i) => !context.IsFailed(i))
            .Select(item => item.Id)
            .ToHashSet();
        Dictionary<Guid, ProductEntity> productMap = (
            await repository.ListAsync(new ProductsByIdsWithLinksSpecification(requestedIds), ct)
        ).ToDictionary(product => product.Id);

        await context.ApplyRulesAsync(
            ct,
            new MarkMissingByIdBatchRule<UpdateProductItem>(
                item => item.Id,
                productMap.Keys.ToHashSet(),
                ErrorCatalog.Products.NotFoundMessage
            )
        );

        context.AddFailures(
            await ProductValidationHelper.CheckProductReferencesAsync(
                items,
                categoryRepository,
                productDataRepository,
                context.FailedIndices,
                ct
            )
        );

        for (int i = 0; i < items.Count; i++)
        {
            if (context.IsFailed(i))
                continue;

            ErrorOr<Price> priceResult = Price.Create(items[i].Price);
            if (priceResult.IsError)
                context.AddFailure(i, items[i].Id, priceResult.FirstError.Description);
        }

        OutgoingMessages messages = new();

        if (context.HasFailures)
        {
            messages.RespondToSender(context.ToFailureResponse());
            return (HandlerContinuation.Stop, null, messages);
        }

        return (
            HandlerContinuation.Continue,
            new EntityLookup<ProductEntity>(productMap),
            messages
        );
    }

    /// <summary>Applies changes and syncs product-data links in a single transaction.</summary>
    public static async Task<(ErrorOr<BatchResponse>, OutgoingMessages)> HandleAsync(
        UpdateProductsCommand command,
        EntityLookup<ProductEntity> lookup,
        ProductRepositoryContract repository,
        IUnitOfWork<ProductCatalogDbMarker> unitOfWork,
        CancellationToken ct
    )
    {
        IReadOnlyList<UpdateProductItem> items = command.Request.Items;
        IReadOnlyDictionary<Guid, ProductEntity> productMap = lookup.Entities;

        await unitOfWork.ExecuteInTransactionAsync(
            async () =>
            {
                for (int i = 0; i < items.Count; i++)
                {
                    UpdateProductItem item = items[i];
                    ProductEntity product = productMap[item.Id];

                    Price price = Price.FromPersistence(item.Price);
                    product.UpdateDetails(item.Name, item.Description, price, item.CategoryId);

                    if (item.ProductDataIds is not null)
                        product.SyncProductDataLinks(item.ProductDataIds);

                    await repository.UpdateAsync(product, ct);
                }
            },
            ct
        );

        OutgoingMessages messages = new();
        messages.Add(new CacheInvalidationNotification(CacheTags.Products));
        messages.Add(new CacheInvalidationNotification(CacheTags.Categories));
        return (new BatchResponse([], items.Count, 0), messages);
    }
}
