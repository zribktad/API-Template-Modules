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
        IProductDataRepository repository,
        ITenantProvider tenantProvider,
        IActorProvider actorProvider,
        TimeProvider timeProvider,
        CancellationToken ct
    )
    {
        Guid tenantId = tenantProvider.TenantId;

        Entities.ProductData.ProductData? data = await repository.GetByIdAsync(command.Id, ct);

        if (data is null || data.TenantId != tenantId)
        {
            OutgoingMessages failureMessages = new();
            failureMessages.RespondToSender(DomainErrors.ProductData.NotFound(command.Id));
            return (HandlerContinuation.Stop, null, failureMessages);
        }

        return (
            HandlerContinuation.Continue,
            new DeleteProductDataState(
                data,
                tenantId,
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
        await unitOfWork.ExecuteInTransactionAsync(
            async () =>
            {
                await productDataLinkRepository.SoftDeleteActiveLinksForProductDataAsync(
                    command.Id,
                    ct
                );
            },
            ct
        );

        await productDataRepository.SoftDeleteAsync(
            command.Id,
            state.ActorId,
            state.DeletedAtUtc,
            ct
        );

        OutgoingMessages messages = new();
        messages.AddRange(CacheInvalidationCascades.ForProductDataDeletion);
        return (Result.Success, messages);
    }

    public sealed record DeleteProductDataState(
        Entities.ProductData.ProductData Data,
        Guid TenantId,
        Guid ActorId,
        DateTime DeletedAtUtc
    );
}
