using SharedKernel.Application.Context;
using SharedKernel.Application.Errors;
using Contracts.Events;
using SharedKernel.Application.Resilience;
using ProductCatalog.Domain.Interfaces;
using ErrorOr;
using Microsoft.Extensions.Logging;
using Polly.Registry;
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

        ProductCatalog.Domain.Entities.ProductData.ProductData? data = await repository.GetByIdAsync(
            command.Id,
            ct
        );

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
            new OutgoingMessages()
        );
    }

    public static async Task<(ErrorOr<Success>, OutgoingMessages)> HandleAsync(
        DeleteProductDataCommand command,
        DeleteProductDataState state,
        IProductDataRepository repository,
        IProductDataLinkRepository productDataLinkRepository,
        IUnitOfWork unitOfWork,
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
            logger.LogError(
                ex,
                "Failed to soft-delete ProductData document {ProductDataId} for tenant {TenantId}. Related ProductDataLinks may already be soft-deleted in PostgreSQL.",
                state.Data.Id,
                state.TenantId
            );
            throw;
        }

        OutgoingMessages messages = new();
        messages.Add(new CacheInvalidationNotification(CacheTags.ProductData));
        messages.Add(new CacheInvalidationNotification(CacheTags.Products));
        return (Result.Success, messages);
    }
}


