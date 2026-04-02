using ErrorOr;
using Identity.Application.Features.Tenant.DTOs;
using Identity.Application.Features.Tenant.Mappings;
using Identity.Domain;
using Identity.Domain.Interfaces;
using Identity.Domain.ValueObjects;
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
        ErrorOr<TenantCode> codeResult = TenantCode.Create(command.Request.Code);
        if (codeResult.IsError)
            return (codeResult.FirstError, OutgoingMessagesHelper.Empty);

        TenantEntity tenant;
        try
        {
            tenant = await unitOfWork.ExecuteInTransactionAsync(
                async () =>
                {
                    Guid id = Guid.NewGuid();
                    TenantEntity entity = TenantEntity.Create(
                        id,
                        codeResult.Value,
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
