using Microsoft.Extensions.Logging;
using Polly;
using Polly.Registry;
using ProductCatalog.Logging;

namespace ProductCatalog.Features.ProductData.DeleteProductData;

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
        ResiliencePipeline pipeline = resiliencePipelineProvider.GetPipeline(
            ResiliencePipelineKeys.MongoProductDataDelete
        );

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
