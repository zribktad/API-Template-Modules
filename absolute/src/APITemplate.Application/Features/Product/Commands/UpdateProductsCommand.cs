using APITemplate.Application.Common.Batch;
using APITemplate.Application.Common.Events;
using APITemplate.Domain.Entities;
using ErrorOr;
using FluentValidation;
using Wolverine;
using ProductEntity = APITemplate.Domain.Entities.Product;

namespace APITemplate.Application.Features.Product;

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
        IProductRepository repository,
        ICategoryRepository categoryRepository,
        IProductDataRepository productDataRepository,
        IValidator<UpdateProductItem> itemValidator,
        CancellationToken ct
    )
    {
        (BatchResponse? failure, Dictionary<Guid, ProductEntity>? productMap) =
            await UpdateProductsValidator.ValidateAndLoadAsync(
                command,
                repository,
                categoryRepository,
                productDataRepository,
                itemValidator,
                ct
            );

        OutgoingMessages messages = new();

        if (failure is not null)
        {
            messages.RespondToSender(failure);
            return (HandlerContinuation.Stop, null, messages);
        }

        return (
            HandlerContinuation.Continue,
            new EntityLookup<ProductEntity>(productMap!),
            messages
        );
    }

    /// <summary>Applies changes and syncs product-data links in a single transaction.</summary>
    public static async Task<ErrorOr<BatchResponse>> HandleAsync(
        UpdateProductsCommand command,
        EntityLookup<ProductEntity> lookup,
        IProductRepository repository,
        IUnitOfWork unitOfWork,
        IMessageBus bus,
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

                    product.UpdateDetails(item.Name, item.Description, item.Price, item.CategoryId);

                    if (item.ProductDataIds is not null)
                    {
                        HashSet<Guid> targetIds = item.ProductDataIds.ToHashSet();
                        Dictionary<Guid, ProductDataLink> existingById =
                            product.ProductDataLinks.ToDictionary(link => link.ProductDataId);
                        product.SyncProductDataLinks(targetIds, existingById);
                    }

                    await repository.UpdateAsync(product, ct);
                }
            },
            ct
        );

        await bus.PublishAsync(new CacheInvalidationNotification(CacheTags.Products));
        return new BatchResponse([], items.Count, 0);
    }
}
