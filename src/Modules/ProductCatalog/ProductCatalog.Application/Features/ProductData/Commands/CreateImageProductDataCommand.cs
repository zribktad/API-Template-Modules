using SharedKernel.Application.Context;
using Contracts.Events;
using ProductCatalog.Application.Features.ProductData.Mappings;
using ProductCatalog.Domain.Entities;
using ProductCatalog.Domain.Interfaces;
using ErrorOr;
using Wolverine;

namespace ProductCatalog.Application.Features.ProductData;

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
        var entity = new ImageProductData
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

        var created = await repository.CreateAsync(entity, ct);
        OutgoingMessages messages = new();
        messages.Add(new CacheInvalidationNotification(CacheTags.ProductData));
        return (created.ToResponse(), messages);
    }
}


