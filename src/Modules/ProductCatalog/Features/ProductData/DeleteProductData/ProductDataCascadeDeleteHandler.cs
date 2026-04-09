using Microsoft.Extensions.Logging;
using ProductCatalog.Logging;

namespace ProductCatalog.Features.ProductData.DeleteProductData;

public sealed class ProductDataCascadeDeleteHandler
{
    public static async Task HandleAsync(
        TenantSoftDeletedNotification @event,
        IProductDataRepository productDataRepository,
        ILogger<ProductDataCascadeDeleteHandler> logger,
        CancellationToken ct
    )
    {
        long count = await productDataRepository.SoftDeleteByTenantAsync(
            @event.TenantId,
            @event.ActorId,
            @event.DeletedAtUtc,
            ct
        );

        logger.ProductDataCascadeDeleteSucceeded(count, @event.TenantId);
    }
}
