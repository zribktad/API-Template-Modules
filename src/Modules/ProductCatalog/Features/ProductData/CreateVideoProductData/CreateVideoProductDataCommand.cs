using ErrorOr;
using Wolverine;

namespace ProductCatalog.Features.ProductData.CreateVideoProductData;

public sealed record CreateVideoProductDataCommand(CreateVideoProductDataRequest Request);

public sealed class CreateVideoProductDataCommandHandler
{
    public static async Task<(ErrorOr<ProductDataResponse>, OutgoingMessages)> HandleAsync(
        CreateVideoProductDataCommand command,
        IProductDataRepository repository,
        ITenantProvider tenantProvider,
        TimeProvider timeProvider,
        CancellationToken ct
    )
    {
        VideoProductData entity = new()
        {
            TenantId = tenantProvider.TenantId,
            Title = command.Request.Title,
            Description = command.Request.Description,
            CreatedAt = timeProvider.GetUtcNow().UtcDateTime,
            DurationSeconds = command.Request.DurationSeconds,
            Resolution = command.Request.Resolution,
            Format = command.Request.Format,
            FileSizeBytes = command.Request.FileSizeBytes,
        };

        Entities.ProductData.ProductData created = await repository.CreateAsync(entity, ct);
        OutgoingMessages messages = new();
        messages.Add(new CacheInvalidationNotification(CacheTags.ProductData));
        return (created.ToResponse(), messages);
    }
}
