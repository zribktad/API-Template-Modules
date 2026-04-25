using ErrorOr;
using ProductCatalog.ValueObjects;
using IProductRepository = ProductCatalog.Interfaces.IProductRepository;
using ProductEntity = ProductCatalog.Entities.Product;

namespace ProductCatalog.Features.Product.IdempotentCreate;

public sealed record IdempotentCreateCommand(IdempotentCreateRequest Request);

public sealed class IdempotentCreateCommandHandler
{
    public static async Task<ErrorOr<IdempotentCreateResponse>> HandleAsync(
        IdempotentCreateCommand command,
        IProductRepository repository,
        IUnitOfWork<ProductCatalogDbMarker> unitOfWork,
        IIdGenerator idGenerator,
        CancellationToken ct
    )
    {
        ProductEntity entity = new()
        {
            Id = idGenerator.NewId(),
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
