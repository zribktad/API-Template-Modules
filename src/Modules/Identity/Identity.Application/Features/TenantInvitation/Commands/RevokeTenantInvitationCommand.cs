using ErrorOr;
using Identity.Domain;
using Wolverine;
using TenantInvitationEntity = Identity.Domain.Entities.TenantInvitation;

namespace Identity.Application.Features.TenantInvitation;

public sealed record RevokeTenantInvitationCommand(Guid InvitationId);

public sealed class RevokeTenantInvitationCommandHandler
{
    public static async Task<(ErrorOr<Success>, OutgoingMessages)> HandleAsync(
        RevokeTenantInvitationCommand command,
        ITenantInvitationRepository invitationRepository,
        IUnitOfWork<IdentityDbMarker> unitOfWork,
        CancellationToken ct
    )
    {
        ErrorOr<TenantInvitationEntity> invitationResult =
            await invitationRepository.GetByIdOrError(
                command.InvitationId,
                DomainErrors.Invitations.NotFound(command.InvitationId),
                ct
            );
        if (invitationResult.IsError)
            return (invitationResult.Errors, OutgoingMessagesHelper.Empty);
        TenantInvitationEntity invitation = invitationResult.Value;

        invitation.Revoke();
        await invitationRepository.UpdateAsync(invitation, ct);
        await unitOfWork.CommitAsync(ct);

        OutgoingMessages messages = new();
        messages.Add(new CacheInvalidationNotification(CacheTags.TenantInvitations));
        return (Result.Success, messages);
    }
}
