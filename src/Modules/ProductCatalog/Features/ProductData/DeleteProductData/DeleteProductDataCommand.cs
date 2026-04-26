using ErrorOr;
using ProductCatalog.Interfaces;
using Wolverine;

namespace ProductCatalog.Features.ProductData.DeleteProductData;

public sealed record DeleteProductDataCommand(Guid Id) : IHasId;

public sealed class DeleteProductDataCommandHandler
{
    public static async Task<(
        HandlerContinuation,
        DeleteProductDataState?,
        OutgoingMessages
    )> LoadAsync(
        DeleteProductDataCommand command,
        ITenantProvider tenantProvider,
        IActorProvider actorProvider,
        TimeProvider timeProvider,
        CancellationToken ct
    )
    {
        return (
            HandlerContinuation.Continue,
            new DeleteProductDataState(
                tenantProvider.TenantId,
                actorProvider.ActorId,
                timeProvider.GetUtcNow().UtcDateTime
            ),
            OutgoingMessagesHelper.Empty
        );
    }

    public static async Task<(ErrorOr<Success>, OutgoingMessages)> HandleAsync(
        DeleteProductDataCommand command,
        DeleteProductDataState state,
        IProductDataLinkRepository productDataLinkRepository,
        IProductDataRepository productDataRepository,
        IUnitOfWork<ProductCatalogDbMarker> unitOfWork,
        CancellationToken ct
    )
    {
        bool productDataDeleted = await productDataRepository.SoftDeleteAsync(
            command.Id,
            state.ActorId,
            state.DeletedAtUtc,
            ct
        );

        if (!productDataDeleted)
            return (DomainErrors.ProductData.NotFound(command.Id), OutgoingMessagesHelper.Empty);

        await unitOfWork.ExecuteInTransactionAsync(
            async () =>
            {
                await productDataLinkRepository.SoftDeleteActiveLinksForProductDataAsync(
                    command.Id,
                    state.TenantId,
                    state.ActorId,
                    state.DeletedAtUtc,
                    ct
                );
            },
            ct
        );

        OutgoingMessages messages = new();
        messages.AddRange(CacheInvalidationCascades.ForProductDataDeletion);
        return (Result.Success, messages);
    }

    public sealed record DeleteProductDataState(Guid TenantId, Guid ActorId, DateTime DeletedAtUtc);
}
