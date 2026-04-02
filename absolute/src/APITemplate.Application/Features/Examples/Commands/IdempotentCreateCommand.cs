using APITemplate.Application.Features.Examples.DTOs;
using APITemplate.Application.Features.Product.Repositories;
using APITemplate.Domain.Interfaces;
using ErrorOr;
using ProductEntity = APITemplate.Domain.Entities.Product;

namespace APITemplate.Application.Features.Examples;

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
        var entity = new ProductEntity
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
