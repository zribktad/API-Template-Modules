using ErrorOr;
using Identity.Domain;
using Wolverine;
using TenantEntity = Identity.Domain.Entities.Tenant;

namespace Identity.Application.Features.Tenant;

public sealed record DeleteTenantCommand(Guid Id) : IHasId;

public sealed class DeleteTenantCommandHandler
{
    public static async Task<(ErrorOr<Success>, OutgoingMessages)> HandleAsync(
        DeleteTenantCommand command,
        ITenantRepository repository,
        IUnitOfWork<IdentityDbMarker> unitOfWork,
        IActorProvider actorProvider,
        TimeProvider timeProvider,
        CancellationToken ct
    )
    {
        ErrorOr<TenantEntity> tenantResult = await repository.GetByIdOrError(
            command.Id,
            DomainErrors.Tenants.NotFound(command.Id),
            ct
        );
        if (tenantResult.IsError)
            return (tenantResult.Errors, OutgoingMessagesHelper.Empty);

        await unitOfWork.ExecuteInTransactionAsync(
            async () =>
            {
                await repository.DeleteAsync(tenantResult.Value, ct);
            },
            ct
        );

        OutgoingMessages messages = new();
        messages.Add(
            new TenantSoftDeletedNotification(
                command.Id,
                actorProvider.ActorId,
                timeProvider.GetUtcNow().UtcDateTime
            )
        );
        messages.Add(new CacheInvalidationNotification(CacheTags.Tenants));
        return (Result.Success, messages);
    }
}
