using ErrorOr;
using Identity.Common.Email;
using Identity.Directory.Entities;
using Identity.Directory.Features.TenantInvitation.Mappings;
using Identity.Directory.Options;
using Microsoft.Extensions.Options;
using Wolverine;
using TenantEntity = Identity.Directory.Entities.Tenant;
using TenantInvitationEntity = Identity.Directory.Entities.TenantInvitation;

namespace Identity.Directory.Features.TenantInvitation;

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
        string email = command.Request.Email;
        string normalizedEmail = NormalizedString.Normalize(email);
        TenantInvitationOptions opts = invitationOptions.Value;

        if (await invitationRepository.HasPendingInvitationAsync(normalizedEmail, ct))
        {
            return (
                DomainErrors.Invitations.AlreadyPending(email),
                OutgoingMessagesHelper.Empty
            );
        }

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
                invitation.Email.Value,
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
