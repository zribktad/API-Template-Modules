using ErrorOr;
using Identity.Application.Common.Email;
using Identity.Application.Options;
using Identity.Domain;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Wolverine;
using TenantEntity = Identity.Domain.Entities.Tenant;
using TenantInvitationEntity = Identity.Domain.Entities.TenantInvitation;

namespace Identity.Application.Features.TenantInvitation;

public sealed record ResendTenantInvitationCommand(Guid InvitationId);

public sealed class ResendTenantInvitationCommandHandler
{
    public static async Task<(ErrorOr<Success>, OutgoingMessages)> HandleAsync(
        ResendTenantInvitationCommand command,
        ITenantInvitationRepository invitationRepository,
        ITenantRepository tenantRepository,
        IUnitOfWork<IdentityDbMarker> unitOfWork,
        ISecureTokenGenerator tokenGenerator,
        ITenantProvider tenantProvider,
        TimeProvider timeProvider,
        IOptions<TenantInvitationOptions> invitationOptions,
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

        if (invitation.Status != InvitationStatus.Pending)
            return (DomainErrors.Invitations.NotPending(), OutgoingMessagesHelper.Empty);

        DateTime now = timeProvider.GetUtcNow().UtcDateTime;
        if (invitation.ExpiresAtUtc < now)
            return (DomainErrors.Invitations.ExpiredCreateNew(), OutgoingMessagesHelper.Empty);

        ErrorOr<TenantEntity> tenantResult = await tenantRepository.GetByIdOrError(
            tenantProvider.TenantId,
            DomainErrors.Tenants.NotFound(tenantProvider.TenantId),
            ct
        );
        if (tenantResult.IsError)
            return (tenantResult.Errors, OutgoingMessagesHelper.Empty);
        TenantEntity tenant = tenantResult.Value;

        TenantInvitationOptions opts = invitationOptions.Value;
        string rawToken = tokenGenerator.GenerateToken();
        invitation.RefreshToken(tokenGenerator.HashToken(rawToken));

        await invitationRepository.UpdateAsync(invitation, ct);
        await unitOfWork.CommitAsync(ct);

        string invitationUrl = $"{opts.BaseUrl}/invitations/accept?token={rawToken}";
        int remainingHours = (int)
            Math.Ceiling(
                (invitation.ExpiresAtUtc - timeProvider.GetUtcNow().UtcDateTime).TotalHours
            );

        OutgoingMessages messages = new();
        messages.Add(
            new TenantInvitationCreatedNotification(
                invitation.Id,
                invitation.Email,
                tenant.Name,
                rawToken,
                invitationUrl,
                remainingHours
            )
        );
        messages.Add(new CacheInvalidationNotification(CacheTags.TenantInvitations));
        return (Result.Success, messages);
    }
}
