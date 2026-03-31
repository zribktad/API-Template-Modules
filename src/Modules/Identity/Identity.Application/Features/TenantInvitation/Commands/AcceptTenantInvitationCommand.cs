using Identity.Application.Common.Email;
using Identity.Domain.Enums;
using Identity.Domain.Interfaces;
using SharedKernel.Domain.Interfaces;
using SharedKernel.Application.Errors;
using SharedKernel.Application.Events;
using ErrorOr;
using Wolverine;

namespace Identity.Application.Features.TenantInvitation;

public sealed record AcceptTenantInvitationCommand(string Token);

public sealed class AcceptTenantInvitationCommandHandler
{
    public static async Task<ErrorOr<Success>> HandleAsync(
        AcceptTenantInvitationCommand command,
        ITenantInvitationRepository invitationRepository,
        IUnitOfWork unitOfWork,
        ISecureTokenGenerator tokenGenerator,
        IMessageBus bus,
        TimeProvider timeProvider,
        CancellationToken ct
    )
    {
        var tokenHash = tokenGenerator.HashToken(command.Token);
        var invitation = await invitationRepository.GetValidByTokenHashAsync(tokenHash, ct);

        if (invitation is null)
            return DomainErrors.Invitations.NotFoundOrExpired();

        var now = timeProvider.GetUtcNow().UtcDateTime;

        if (invitation.ExpiresAtUtc < now)
            return DomainErrors.Invitations.Expired();

        if (invitation.Status == InvitationStatus.Accepted)
            return DomainErrors.Invitations.AlreadyAccepted();

        invitation.Status = InvitationStatus.Accepted;
        await invitationRepository.UpdateAsync(invitation, ct);
        await unitOfWork.CommitAsync(ct);

        await bus.PublishAsync(new CacheInvalidationNotification(CacheTags.TenantInvitations));
        return Result.Success;
    }
}
