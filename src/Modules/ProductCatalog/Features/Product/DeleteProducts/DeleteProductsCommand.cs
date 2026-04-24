using ErrorOr;
using Wolverine;
using ProductRepositoryContract = ProductCatalog.Interfaces.IProductRepository;

namespace ProductCatalog.Features.Product.DeleteProducts;

/// <summary>Soft-deletes multiple products and their associated data links in a single batch operation.</summary>
public sealed record DeleteProductsCommand(BatchDeleteRequest Request);

/// <summary>
///     Handles <see cref="DeleteProductsCommand" /> by loading all products, soft-deleting links and products in a
///     single transaction.
/// </summary>
public sealed class DeleteProductsCommandHandler
{
    public static async Task<(
        HandlerContinuation,
        DeleteProductsState?,
        OutgoingMessages
    )> LoadAsync(
        DeleteProductsCommand command,
        ProductRepositoryContract repository,
        IActorProvider actorProvider,
        ITenantProvider tenantProvider,
        TimeProvider timeProvider,
        CancellationToken ct
    )
    {
        IReadOnlyList<Guid> ids = command.Request.Ids;
        Guid actorId = actorProvider.ActorId;
        Guid tenantId = tenantProvider.TenantId;
        DateTime deletedAtUtc = timeProvider.GetUtcNow().UtcDateTime;
        BatchFailureContext<Guid> context = new(ids);

        IReadOnlyList<Entities.Product> products = await repository.ListAsync(
            new ProductsByIdsSpecification(ids),
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

        IReadOnlyList<Guid> productIds = products.Select(p => p.Id).ToList();
        return (
            HandlerContinuation.Continue,
            new DeleteProductsState(productIds, tenantId, actorId, deletedAtUtc),
            OutgoingMessagesHelper.Empty
        );
    }

    public static async Task<(ErrorOr<BatchResponse>, OutgoingMessages)> HandleAsync(
        DeleteProductsCommand command,
        DeleteProductsState state,
        ProductRepositoryContract repository,
        IUnitOfWork<ProductCatalogDbMarker> unitOfWork,
        IProductDataLinkRepository linkRepository,
        CancellationToken ct
    )
    {
        await unitOfWork.ExecuteInTransactionAsync(
            async () =>
            {
                await linkRepository.BulkSoftDeleteByProductIdsAsync(
                    state.ProductIds,
                    state.TenantId,
                    state.ActorId,
                    state.DeletedAtUtc,
                    ct
                );
                await repository.BulkSoftDeleteByIdsAsync(
                    state.ProductIds,
                    state.TenantId,
                    state.ActorId,
                    state.DeletedAtUtc,
                    ct
                );
            },
            ct
        );

        OutgoingMessages messages = new();
        messages.AddRange(CacheInvalidationCascades.ForProductDeletion());
        if (state.ProductIds.Count > 0)
            messages.Add(
                new ProductsBatchSoftDeletedNotification(
                    state.ProductIds,
                    state.TenantId,
                    state.ActorId,
                    state.DeletedAtUtc,
                    Guid.NewGuid()
                )
            );

        return (new BatchResponse([], command.Request.Ids.Count, 0), messages);
    }

    public sealed record DeleteProductsState(
        IReadOnlyList<Guid> ProductIds,
        Guid TenantId,
        Guid ActorId,
        DateTime DeletedAtUtc
    );
}
