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
    public static async Task<(ErrorOr<TenantResponse>, OutgoingMessages)> HandleAsync(
        CreateTenantCommand command,
        ITenantRepository repository,
        IUnitOfWork<IdentityDbMarker> unitOfWork,
        ITenantCodeConflictDetector tenantCodeConflictDetector,
        CancellationToken ct
    )
    {
        TenantEntity tenant;
        try
        {
            tenant = await unitOfWork.ExecuteInTransactionAsync(
                async () =>
                {
                    Guid id = Guid.NewGuid();
                    TenantEntity entity = TenantEntity.Create(
                        id,
                        command.Request.Code,
                        command.Request.Name
                    );

                    await repository.AddAsync(entity, ct);
                    return entity;
                },
                ct
            );
        }
        catch (Exception ex) when (tenantCodeConflictDetector.IsCodeConflict(ex))
        {
            return (
                DomainErrors.Tenants.CodeAlreadyExists(command.Request.Code),
                OutgoingMessagesHelper.Empty
            );
        }

        OutgoingMessages messages = new();
        messages.Add(new CacheInvalidationNotification(CacheTags.Tenants));
        return (tenant.ToResponse(), messages);
    }
}
