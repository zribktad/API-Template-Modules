using ErrorOr;
using ProductCatalog.Features.Product.Specifications;
using ProductCatalog;
using Wolverine;
using ProductRepositoryContract = ProductCatalog.Features.Product.Repositories.IProductRepository;

namespace ProductCatalog.Features.Product;

/// <summary>Soft-deletes multiple products and their associated data links in a single batch operation.</summary>
public sealed record DeleteProductsCommand(BatchDeleteRequest Request);

/// <summary>Handles <see cref="DeleteProductsCommand"/> by loading all products, soft-deleting links and products in a single transaction.</summary>
public sealed class DeleteProductsCommandHandler
{
    public sealed record DeleteProductsState(
        IReadOnlyList<ProductCatalog.Entities.Product> Products,
        Guid ActorId,
        DateTime DeletedAtUtc
    );

    public static async Task<(
        HandlerContinuation,
        DeleteProductsState?,
        OutgoingMessages
    )> LoadAsync(
        DeleteProductsCommand command,
        ProductRepositoryContract repository,
        IActorProvider actorProvider,
        TimeProvider timeProvider,
        CancellationToken ct
    )
    {
        IReadOnlyList<Guid> ids = command.Request.Ids;
        Guid actorId = actorProvider.ActorId;
        DateTime deletedAtUtc = timeProvider.GetUtcNow().UtcDateTime;
        BatchFailureContext<Guid> context = new(ids);

        // Load all target products and mark missing ones as failed
        IReadOnlyList<ProductCatalog.Entities.Product> products = await repository.ListAsync(
            new ProductsByIdsWithLinksSpecification(ids.ToHashSet()),
            ct
        );

        await context.ApplyRulesAsync(
            ct,
            new MarkMissingByIdBatchRule<Guid>(
                id => id,
                products.Select(product => product.Id).ToHashSet(),
                ErrorCatalog.Products.NotFoundMessage
            )
        );

        if (context.HasFailures)
        {
            OutgoingMessages failureMessages = new();
            failureMessages.RespondToSender(context.ToFailureResponse());
            return (HandlerContinuation.Stop, null, failureMessages);
        }

        return (
            HandlerContinuation.Continue,
            new DeleteProductsState(products, actorId, deletedAtUtc),
            OutgoingMessagesHelper.Empty
        );
    }

    public static async Task<(ErrorOr<BatchResponse>, OutgoingMessages)> HandleAsync(
        DeleteProductsCommand command,
        DeleteProductsState state,
        ProductRepositoryContract repository,
        IUnitOfWork<ProductCatalogDbMarker> unitOfWork,
        CancellationToken ct
    )
    {
        // Soft-delete product-data links and remove products in a single transaction
        await unitOfWork.ExecuteInTransactionAsync(
            async () =>
            {
                await repository.DeleteRangeAsync(state.Products, ct);
            },
            ct
        );

        OutgoingMessages messages = new();
        messages.Add(new CacheInvalidationNotification(CacheTags.Products));
        messages.Add(new CacheInvalidationNotification(CacheTags.Categories));
        messages.Add(new CacheInvalidationNotification(CacheTags.Reviews));
        foreach (Guid productId in state.Products.Select(product => product.Id))
        {
            messages.Add(
                new ProductSoftDeletedNotification(productId, state.ActorId, state.DeletedAtUtc)
            );
        }

        return (new BatchResponse([], command.Request.Ids.Count, 0), messages);
    }
}

