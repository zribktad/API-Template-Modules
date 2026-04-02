using ErrorOr;
using Identity.Application.Common.Email;
using Identity.Domain;
using Wolverine;
using TenantInvitationEntity = Identity.Domain.Entities.TenantInvitation;

namespace Identity.Application.Features.TenantInvitation;

public sealed record AcceptTenantInvitationCommand(string Token);

public sealed class AcceptTenantInvitationCommandHandler
{
    public static async Task<(ErrorOr<Success>, OutgoingMessages)> HandleAsync(
        AcceptTenantInvitationCommand command,
        ITenantInvitationRepository invitationRepository,
        IUnitOfWork<IdentityDbMarker> unitOfWork,
        ISecureTokenGenerator tokenGenerator,
        TimeProvider timeProvider,
        CancellationToken ct
    )
    {
        string tokenHash = tokenGenerator.HashToken(command.Token);
        TenantInvitationEntity? invitation = await invitationRepository.GetValidByTokenHashAsync(
            tokenHash,
            ct
        );

        if (invitation is null)
            return (DomainErrors.Invitations.NotFoundOrExpired(), OutgoingMessagesHelper.Empty);

        ErrorOr<Success> acceptResult = invitation.Accept(timeProvider);
        if (acceptResult.IsError)
            return (acceptResult.Errors, OutgoingMessagesHelper.Empty);
        await invitationRepository.UpdateAsync(invitation, ct);
        await unitOfWork.CommitAsync(ct);

        OutgoingMessages messages = new();
        messages.Add(new CacheInvalidationNotification(CacheTags.TenantInvitations));
        return (Result.Success, messages);
    }
}
