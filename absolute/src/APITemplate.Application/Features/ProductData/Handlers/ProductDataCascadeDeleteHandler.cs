using APITemplate.Application.Common.Events;
using APITemplate.Application.Common.Resilience;
using Microsoft.Extensions.Logging;
using Polly.Registry;

namespace APITemplate.Application.Features.ProductData.Handlers;

public sealed class ProductDataCascadeDeleteHandler
{
    public static async Task HandleAsync(
        TenantSoftDeletedNotification @event,
        IProductDataRepository productDataRepository,
        ResiliencePipelineProvider<string> resiliencePipelineProvider,
        ILogger<ProductDataCascadeDeleteHandler> logger,
        CancellationToken ct
    )
    {
        var pipeline = resiliencePipelineProvider.GetPipeline(
            ResiliencePipelineKeys.MongoProductDataDelete
        );

        try
        {
            var count = await pipeline.ExecuteAsync(
                async token =>
                    await productDataRepository.SoftDeleteByTenantAsync(
                        @event.TenantId,
                        @event.ActorId,
                        @event.DeletedAtUtc,
                        token
                    ),
                ct
            );

            logger.LogInformation(
                "Cascade soft-deleted {Count} ProductData documents for tenant {TenantId}.",
                count,
                @event.TenantId
            );
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Failed to cascade soft-delete ProductData documents for tenant {TenantId}. EF entities are already soft-deleted.",
                @event.TenantId
            );
        }
    }
}
