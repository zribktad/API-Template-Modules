using ErrorOr;
using ProductCatalog;
using ProductCatalog.ValueObjects;
using IProductRepository = ProductCatalog.Interfaces.IProductRepository;
using ProductEntity = ProductCatalog.Entities.Product;

namespace ProductCatalog.Features.IdempotentCreate;

public sealed record IdempotentCreateCommand(IdempotentCreateRequest Request);

public sealed class IdempotentCreateCommandHandler
{
    public static async Task<ErrorOr<IdempotentCreateResponse>> HandleAsync(
        IdempotentCreateCommand command,
        IProductRepository repository,
        IUnitOfWork<ProductCatalogDbMarker> unitOfWork,
        CancellationToken ct
    )
    {
        ProductEntity entity = new()
        {
            Id = Guid.NewGuid(),
            Name = command.Request.Name,
            Description = command.Request.Description,
            Price = Price.Zero,
        };

        await unitOfWork.ExecuteInTransactionAsync(
            async () =>
            {
                await repository.AddAsync(entity, ct);
            },
            ct
        );

        return new IdempotentCreateResponse(
            entity.Id,
            entity.Name,
            entity.Description,
            entity.Audit.CreatedAtUtc
        );
    }
}
