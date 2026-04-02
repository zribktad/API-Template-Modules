using Contracts.Events;
using ErrorOr;
using ProductCatalog.Domain;
using ProductCatalog.Domain.Entities;
using ProductCatalog.Domain.ValueObjects;
using SharedKernel.Application.Batch;
using Wolverine;
using ProductEntity = ProductCatalog.Domain.Entities.Product;
using ProductRepositoryContract = ProductCatalog.Application.Features.Product.Repositories.IProductRepository;

namespace ProductCatalog.Application.Features.Product;

/// <summary>Creates multiple products in a single batch operation.</summary>
public sealed record CreateProductsCommand(CreateProductsRequest Request);

/// <summary>Handles <see cref="CreateProductsCommand"/> by validating all items, bulk-validating references, and persisting in a single transaction.</summary>
public sealed class CreateProductsCommandHandler
{
    public static async Task<(
        HandlerContinuation,
        IReadOnlyList<ProductEntity>?,
        OutgoingMessages
    )> LoadAsync(
        CreateProductsCommand command,
        ICategoryRepository categoryRepository,
        IProductDataRepository productDataRepository,
        IBatchRule<CreateProductRequest> itemValidationRule,
        CancellationToken ct
    )
    {
        IReadOnlyList<CreateProductRequest> items = command.Request.Items;
        BatchFailureContext<CreateProductRequest> context = new(items);

        await context.ApplyRulesAsync(ct, itemValidationRule);

        // Reference checks skip only fluent-validation failures so both category and
        // product-data issues can be reported for the same index (merged into one failure row).
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
                context.AddFailure(i, null, priceResult.FirstError.Description);
        }

        if (context.HasFailures)
        {
            OutgoingMessages failureMessages = new();
            failureMessages.RespondToSender(context.ToFailureResponse());
            return (HandlerContinuation.Stop, null, failureMessages);
        }

        List<ProductEntity> entities = items
            .Select(item =>
            {
                Price price = Price.Create(item.Price).Value;
                return ProductEntity.Create(
                    item.Name,
                    item.Description,
                    price,
                    item.CategoryId,
                    item.ProductDataIds
                );
            })
            .ToList();

        return (HandlerContinuation.Continue, entities, OutgoingMessagesHelper.Empty);
    }

    public static async Task<(ErrorOr<BatchResponse>, OutgoingMessages)> HandleAsync(
        CreateProductsCommand command,
        IReadOnlyList<ProductEntity> entities,
        ProductRepositoryContract repository,
        IUnitOfWork<ProductCatalogDbMarker> unitOfWork,
        CancellationToken ct
    )
    {
        await unitOfWork.ExecuteInTransactionAsync(
            async () =>
            {
                await repository.AddRangeAsync(entities, ct);
            },
            ct
        );

        OutgoingMessages messages = new();
        messages.Add(new CacheInvalidationNotification(CacheTags.Products));
        messages.Add(new CacheInvalidationNotification(CacheTags.Categories));
        return (new BatchResponse([], command.Request.Items.Count, 0), messages);
    }
}
