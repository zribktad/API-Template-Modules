using ErrorOr;
using Identity.Features.Tenant.Mappings;
using Identity.ValueObjects;
using Wolverine;
using TenantEntity = Identity.Entities.Tenant;

namespace Identity.Features.Tenant;

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
                DomainErrors.Tenants.CodeAlreadyExists(codeResult.Value),
                OutgoingMessagesHelper.Empty
            );
        }

        OutgoingMessages messages = new();
        messages.Add(new CacheInvalidationNotification(CacheTags.Tenants));
        return (tenant.ToResponse(), messages);
    }
}
