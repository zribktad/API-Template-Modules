using ErrorOr;
using Wolverine;

namespace ProductCatalog.Features.ProductData.CreateImageProductData;

public sealed record CreateImageProductDataCommand(CreateImageProductDataRequest Request);

public sealed class CreateImageProductDataCommandHandler
{
    public static async Task<(ErrorOr<ProductDataResponse>, OutgoingMessages)> HandleAsync(
        CreateImageProductDataCommand command,
        IProductDataRepository repository,
        ITenantProvider tenantProvider,
        TimeProvider timeProvider,
        CancellationToken ct
    )
    {
        ImageProductData entity = new()
        {
            TenantId = tenantProvider.TenantId,
            Title = command.Request.Title,
            Description = command.Request.Description,
            CreatedAt = timeProvider.GetUtcNow().UtcDateTime,
            Width = command.Request.Width,
            Height = command.Request.Height,
            Format = command.Request.Format,
            FileSizeBytes = command.Request.FileSizeBytes,
        };

        Entities.ProductData.ProductData created = await repository.CreateAsync(entity, ct);
        OutgoingMessages messages = new();
        messages.Add(new CacheInvalidationNotification(CacheTags.ProductData));
        return (created.ToResponse(), messages);
    }
}
