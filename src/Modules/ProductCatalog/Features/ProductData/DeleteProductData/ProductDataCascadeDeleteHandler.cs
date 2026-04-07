using Microsoft.Extensions.Logging;
using Polly;
using ProductCatalog.Interfaces;
using ProductCatalog.Logging;

namespace ProductCatalog.Features.ProductData.DeleteProductData;

public sealed class ProductDataCascadeDeleteHandler
{
    public static async Task HandleAsync(
        TenantSoftDeletedNotification @event,
        IProductDataRepository productDataRepository,
        IMongoProductDataDeletePipelineProvider pipelineProvider,
        ILogger<ProductDataCascadeDeleteHandler> logger,
        CancellationToken ct
    )
    {
        ResiliencePipeline pipeline = pipelineProvider.Get();

        try
        {
            long count = await pipeline.ExecuteAsync(
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