namespace ProductCatalog.Features.ProductData.DeleteProductData;

public sealed class SoftDeleteProductDataMongoHandler
{
    public static async Task HandleAsync(
        SoftDeleteProductDataMongoEvent @event,
        IProductDataRepository productDataRepository,
        CancellationToken ct
    )
    {
        await productDataRepository.SoftDeleteAsync(
            @event.ProductDataId,
            @event.ActorId,
            @event.DeletedAtUtc,
            ct
        );
    }
}
