using APITemplate.Api.Features.Examples.DTOs;
using ErrorOr;
using ProductCatalog.Domain.Interfaces;
using SharedKernel.Domain.Interfaces;
using ProductEntity = ProductCatalog.Domain.Entities.Product;

namespace APITemplate.Api.Features.Examples.Commands;

public sealed record IdempotentCreateCommand(IdempotentCreateRequest Request);

public sealed class IdempotentCreateCommandHandler
{
    public static async Task<ErrorOr<IdempotentCreateResponse>> HandleAsync(
        IdempotentCreateCommand command,
        IProductRepository repository,
        IUnitOfWork unitOfWork,
        CancellationToken ct
    )
    {
        ProductEntity entity = new()
        {
            Id = Guid.NewGuid(),
            Name = command.Request.Name,
            Description = command.Request.Description,
            Price = 0,
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
