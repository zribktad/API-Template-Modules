using ErrorOr;
using Identity.Application.Common.Email;
using Identity.Application.Features.TenantInvitation.DTOs;
using Identity.Application.Features.TenantInvitation.Mappings;
using Identity.Application.Options;
using Identity.Domain;
using Identity.Domain.Entities;
using Identity.Domain.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SharedKernel.Application.Context;
using SharedKernel.Application.Events;
using SharedKernel.Application.Extensions;
using SharedKernel.Domain.Interfaces;
using Wolverine;
using TenantEntity = Identity.Domain.Entities.Tenant;
using TenantInvitationEntity = Identity.Domain.Entities.TenantInvitation;

namespace Identity.Application.Features.TenantInvitation;

public sealed record CreateTenantInvitationCommand(CreateTenantInvitationRequest Request);

public sealed class CreateTenantInvitationCommandHandler
{
    public static async Task<(ErrorOr<TenantInvitationResponse>, OutgoingMessages)> HandleAsync(
        CreateTenantInvitationCommand command,
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
        TenantInvitationOptions opts = invitationOptions.Value;
        string normalizedEmail = AppUser.NormalizeEmail(command.Request.Email);

        if (await invitationRepository.HasPendingInvitationAsync(normalizedEmail, ct))
            return (
                DomainErrors.Invitations.AlreadyPending(command.Request.Email),
                OutgoingMessagesHelper.Empty
            );

        ErrorOr<TenantEntity> tenantResult = await tenantRepository.GetByIdOrError(
            tenantProvider.TenantId,
            DomainErrors.Tenants.NotFound(tenantProvider.TenantId),
            ct
        );
        if (tenantResult.IsError)
            return (tenantResult.Errors, OutgoingMessagesHelper.Empty);
        TenantEntity tenant = tenantResult.Value;

        string rawToken = tokenGenerator.GenerateToken();
        string tokenHash = tokenGenerator.HashToken(rawToken);

        TenantInvitationEntity invitation = TenantInvitationEntity.Create(
            command.Request.Email,
            tokenHash,
            opts.InvitationTokenExpiryHours,
            timeProvider
        );

        await invitationRepository.AddAsync(invitation, ct);
        await unitOfWork.CommitAsync(ct);

        string invitationUrl = $"{opts.BaseUrl}/invitations/accept?token={rawToken}";

        OutgoingMessages messages = new();
        messages.Add(
            new TenantInvitationCreatedNotification(
                invitation.Id,
                invitation.Email,
                tenant.Name,
                rawToken,
                invitationUrl,
                opts.InvitationTokenExpiryHours
            )
        );
        messages.Add(new CacheInvalidationNotification(CacheTags.TenantInvitations));
        return (invitation.ToResponse(), messages);
    }
}
