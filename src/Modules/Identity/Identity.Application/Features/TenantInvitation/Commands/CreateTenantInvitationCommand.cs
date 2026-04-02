using ErrorOr;
using Identity.Application.Common.Email;
using Identity.Application.Features.TenantInvitation.Mappings;
using Identity.Application.Options;
using Identity.Domain;
using Identity.Domain.ValueObjects;
using Microsoft.Extensions.Options;
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
        ErrorOr<Email> emailResult = Email.Create(command.Request.Email);
        if (emailResult.IsError)
            return (emailResult.Errors, OutgoingMessagesHelper.Empty);
        Email email = emailResult.Value;

        TenantInvitationOptions opts = invitationOptions.Value;

        if (await invitationRepository.HasPendingInvitationAsync(email.Normalize(), ct))
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
            email,
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
