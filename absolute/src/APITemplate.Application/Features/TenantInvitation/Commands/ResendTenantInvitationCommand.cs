using APITemplate.Application.Common.Context;
using APITemplate.Application.Common.Email;
using APITemplate.Application.Common.Errors;
using APITemplate.Application.Common.Events;
using APITemplate.Application.Common.Extensions;
using APITemplate.Domain.Enums;
using APITemplate.Domain.Interfaces;
using ErrorOr;
using Microsoft.Extensions.Logging;
using Wolverine;

namespace APITemplate.Application.Features.TenantInvitation;

public sealed record ResendTenantInvitationCommand(Guid InvitationId);

public sealed class ResendTenantInvitationCommandHandler
{
    public static async Task<ErrorOr<Success>> HandleAsync(
        ResendTenantInvitationCommand command,
        ITenantInvitationRepository invitationRepository,
        ITenantRepository tenantRepository,
        IUnitOfWork unitOfWork,
        ISecureTokenGenerator tokenGenerator,
        IMessageBus bus,
        ITenantProvider tenantProvider,
        TimeProvider timeProvider,
        ILogger<ResendTenantInvitationCommandHandler> logger,
        CancellationToken ct
    )
    {
        var invitationResult = await invitationRepository.GetByIdOrError(
            command.InvitationId,
            DomainErrors.Invitations.NotFound(command.InvitationId),
            ct
        );
        if (invitationResult.IsError)
            return invitationResult.Errors;
        var invitation = invitationResult.Value;

        if (invitation.Status != InvitationStatus.Pending)
            return DomainErrors.Invitations.NotPending();

        var now = timeProvider.GetUtcNow().UtcDateTime;
        if (invitation.ExpiresAtUtc < now)
            return DomainErrors.Invitations.ExpiredCreateNew();

        var tenantResult = await tenantRepository.GetByIdOrError(
            tenantProvider.TenantId,
            DomainErrors.Tenants.NotFound(tenantProvider.TenantId),
            ct
        );
        if (tenantResult.IsError)
            return tenantResult.Errors;
        var tenant = tenantResult.Value;

        var rawToken = tokenGenerator.GenerateToken();
        invitation.TokenHash = tokenGenerator.HashToken(rawToken);

        await invitationRepository.UpdateAsync(invitation, ct);
        await unitOfWork.CommitAsync(ct);

        await bus.PublishSafeAsync(
            new TenantInvitationCreatedNotification(
                invitation.Id,
                invitation.Email,
                tenant.Name,
                rawToken
            ),
            logger
        );

        await bus.PublishAsync(new CacheInvalidationNotification(CacheTags.TenantInvitations));
        return Result.Success;
    }
}
