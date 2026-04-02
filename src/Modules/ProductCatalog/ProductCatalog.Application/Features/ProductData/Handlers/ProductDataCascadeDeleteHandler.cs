using Microsoft.Extensions.Logging;
using Polly.Registry;
using ProductCatalog.Application.Logging;
using SharedKernel.Application.Resilience;

namespace ProductCatalog.Application.Features.ProductData.Handlers;

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

            logger.ProductDataCascadeDeleteSucceeded(count, @event.TenantId);
        }
        catch (Exception ex)
        {
            logger.ProductDataCascadeDeleteFailed(ex, @event.TenantId);
        }
    }
}
