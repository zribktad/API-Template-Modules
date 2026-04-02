using ErrorOr;
using Microsoft.Extensions.Logging;
using Polly.Registry;
using ProductCatalog.Application.Logging;
using ProductCatalog.Domain;
using Wolverine;

namespace ProductCatalog.Application.Features.ProductData;

public sealed record DeleteProductDataCommand(Guid Id) : IHasId;

public sealed class DeleteProductDataCommandHandler
{
    public sealed record DeleteProductDataState(
        ProductCatalog.Domain.Entities.ProductData.ProductData Data,
        Guid TenantId,
        Guid ActorId,
        DateTime DeletedAtUtc
    );

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

        ProductCatalog.Domain.Entities.ProductData.ProductData? data =
            await repository.GetByIdAsync(command.Id, ct);

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
        IProductDataRepository repository,
        IProductDataLinkRepository productDataLinkRepository,
        IUnitOfWork<ProductCatalogDbMarker> unitOfWork,
        ResiliencePipelineProvider<string> resiliencePipelineProvider,
        ILogger<DeleteProductDataCommandHandler> logger,
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

        var pipeline = resiliencePipelineProvider.GetPipeline(
            ResiliencePipelineKeys.MongoProductDataDelete
        );

        try
        {
            await pipeline.ExecuteAsync(
                async token =>
                    await repository.SoftDeleteAsync(
                        state.Data.Id,
                        state.ActorId,
                        state.DeletedAtUtc,
                        token
                    ),
                ct
            );
        }
        catch (Exception ex)
        {
            logger.ProductDataSoftDeleteFailed(ex, state.Data.Id, state.TenantId);
            throw;
        }

        OutgoingMessages messages = new();
        messages.Add(new CacheInvalidationNotification(CacheTags.ProductData));
        messages.Add(new CacheInvalidationNotification(CacheTags.Products));
        return (Result.Success, messages);
    }
}
