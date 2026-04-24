using ErrorOr;
using ProductCatalog.Domain.Services;
using ProductCatalog.ValueObjects;
using Wolverine;
using ProductEntity = ProductCatalog.Entities.Product;
using ProductRepositoryContract = ProductCatalog.Interfaces.IProductRepository;

namespace ProductCatalog.Features.Product.UpdateProducts;

/// <summary>Updates multiple products in a single batch operation.</summary>
public sealed record UpdateProductsCommand(UpdateProductsRequest Request);

/// <summary>
///     Handles <see cref="UpdateProductsCommand" /> by delegating validation to
///     <see cref="IProductBatchValidator{T}" />, loading affected products in bulk, and updating in a single
///     transaction.
/// </summary>
public sealed class UpdateProductsCommandHandler
{
    /// <summary>
    ///     Wolverine compound-handler load step: pre-loads existing products, validates the batch (including
    ///     "missing id" detection) and short-circuits the pipeline with a failure response when any rule fails.
    /// </summary>
    public static async Task<(
        HandlerContinuation,
        EntityLookup<ProductEntity>?,
        OutgoingMessages
    )> LoadAsync(
        UpdateProductsCommand command,
        ProductRepositoryContract repository,
        IProductBatchValidator<UpdateProductItem> validator,
        IBatchRule<UpdateProductItem> itemValidationRule,
        CancellationToken ct
    )
    {
        IReadOnlyList<UpdateProductItem> items = command.Request.Items;

        // Pre-apply item validation so malformed rows (e.g. Guid.Empty Id) are skipped before
        // hitting the repository. The validator re-runs the same rule later as part of the full
        // batch validation — that's an idempotent in-memory check, so the duplication is cheap.
        BatchFailureContext<UpdateProductItem> preValidation = new(items);
        await preValidation.ApplyRulesAsync(ct, itemValidationRule);

        HashSet<Guid> requestedIds = items
            .Where((item, i) => !preValidation.IsFailed(i) && item.Id != Guid.Empty)
            .Select(item => item.Id)
            .ToHashSet();

        Dictionary<Guid, ProductEntity> productMap =
            requestedIds.Count == 0
                ? []
                : (
                    await repository.ListAsync(
                        new ProductsByIdsWithLinksSpecification(requestedIds),
                        ct
                    )
                ).ToDictionary(product => product.Id);

        MarkMissingByIdBatchRule<UpdateProductItem> missingRule = new(
            item => item.Id,
            productMap.Keys.ToHashSet(),
            ErrorCatalog.Products.NotFoundMessage
        );

        ErrorOr<IReadOnlyList<Price>> validation = await validator.ValidateAsync(
            items,
            ct,
            missingRule
        );

        OutgoingMessages messages = new();
        if (validation.IsError)
        {
            messages.RespondToSender(BatchResponseError.Unwrap(validation.FirstError));
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

                    // Price was validated in LoadAsync via IProductBatchValidator; FromPersistence bypasses
                    // re-validation safely.
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
        messages.AddRange(CacheInvalidationCascades.ForProductChange);
        return (new BatchResponse([], items.Count, 0), messages);
    }
}
