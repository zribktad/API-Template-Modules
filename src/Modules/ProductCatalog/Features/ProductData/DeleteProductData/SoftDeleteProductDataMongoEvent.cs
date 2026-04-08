namespace ProductCatalog.Features.ProductData.DeleteProductData;

public sealed record SoftDeleteProductDataMongoEvent(
    Guid ProductDataId,
    Guid ActorId,
    DateTime DeletedAtUtc
);
