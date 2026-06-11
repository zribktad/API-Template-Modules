using ErrorOr;
using Wolverine;
using TenantEntity = Identity.Directory.Entities.Tenant;

namespace Identity.Directory.Features.Tenant;

public sealed record DeleteTenantCommand(Guid Id) : IHasId;

public sealed class DeleteTenantCommandHandler
{
    public static async Task<(ErrorOr<Success>, OutgoingMessages)> HandleAsync(
        DeleteTenantCommand command,
        ITenantRepository repository,
        IUserRepository userRepository,
        ITenantInvitationRepository invitationRepository,
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
                List<AppUser> users = await userRepository.ListAsync(
                    new UsersForTenantSoftDeleteSpecification(command.Id),
                    ct
                );

                List<Entities.TenantInvitation> invitations = await invitationRepository.ListAsync(
                    new InvitationsForTenantSoftDeleteSpecification(command.Id),
                    ct
                );

                if (users.Count > 0)
                    await userRepository.DeleteRangeAsync(users, ct);

                if (invitations.Count > 0)
                    await invitationRepository.DeleteRangeAsync(invitations, ct);

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
        messages.Add(new CacheInvalidationNotification(CacheTags.Tenants, Guid.Empty));
        return (Result.Success, messages);
    }
}
