using ErrorOr;
using Identity.Application.Features.Tenant.DTOs;
using Identity.Application.Features.Tenant.Mappings;
using Identity.Domain;
using Identity.Domain.Interfaces;
using SharedKernel.Application.Events;
using SharedKernel.Domain.Interfaces;
using Wolverine;
using TenantEntity = Identity.Domain.Entities.Tenant;

namespace Identity.Application.Features.Tenant;

public sealed record CreateTenantCommand(CreateTenantRequest Request);

public sealed class CreateTenantCommandHandler
{
    public static async Task<ErrorOr<TenantResponse>> HandleAsync(
        CreateTenantCommand command,
        ITenantRepository repository,
        IUnitOfWork<IdentityDbMarker> unitOfWork,
        IMessageBus bus,
        CancellationToken ct
    )
    {
        if (await repository.CodeExistsAsync(command.Request.Code, ct))
            return DomainErrors.Tenants.CodeAlreadyExists(command.Request.Code);

        var tenant = await unitOfWork.ExecuteInTransactionAsync(
            async () =>
            {
                var id = Guid.NewGuid();
                var entity = new TenantEntity
                {
                    Id = id,
                    TenantId = id,
                    Code = command.Request.Code,
                    Name = command.Request.Name,
                };

                await repository.AddAsync(entity, ct);
                return entity;
            },
            ct
        );

        await bus.PublishAsync(new CacheInvalidationNotification(CacheTags.Tenants));
        return tenant.ToResponse();
    }
}
