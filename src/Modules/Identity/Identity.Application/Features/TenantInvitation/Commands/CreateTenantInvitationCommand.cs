using ErrorOr;
using Identity.Application.Common.Email;
using Identity.Application.Features.TenantInvitation.DTOs;
using Identity.Application.Features.TenantInvitation.Mappings;
using Identity.Domain;
using Identity.Domain.Entities;
using Identity.Domain.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SharedKernel.Application.Context;
using SharedKernel.Application.Events;
using SharedKernel.Application.Extensions;
using SharedKernel.Application.Options.Infrastructure;
using SharedKernel.Domain.Interfaces;
using Wolverine;
using TenantEntity = Identity.Domain.Entities.Tenant;
using TenantInvitationEntity = Identity.Domain.Entities.TenantInvitation;

namespace Identity.Application.Features.TenantInvitation;

public sealed record CreateTenantInvitationCommand(CreateTenantInvitationRequest Request);

public sealed class CreateTenantInvitationCommandHandler
{
    public static async Task<ErrorOr<TenantInvitationResponse>> HandleAsync(
        CreateTenantInvitationCommand command,
        ITenantInvitationRepository invitationRepository,
        ITenantRepository tenantRepository,
        IUnitOfWork<IdentityDbMarker> unitOfWork,
        ISecureTokenGenerator tokenGenerator,
        IMessageBus bus,
        ITenantProvider tenantProvider,
        TimeProvider timeProvider,
        IOptions<EmailOptions> emailOptions,
        ILogger<CreateTenantInvitationCommandHandler> logger,
        CancellationToken ct
    )
    {
        EmailOptions emailOpts = emailOptions.Value;
        string normalizedEmail = AppUser.NormalizeEmail(command.Request.Email);

        if (await invitationRepository.HasPendingInvitationAsync(normalizedEmail, ct))
            return DomainErrors.Invitations.AlreadyPending(command.Request.Email);

        ErrorOr<TenantEntity> tenantResult = await tenantRepository.GetByIdOrError(
            tenantProvider.TenantId,
            DomainErrors.Tenants.NotFound(tenantProvider.TenantId),
            ct
        );
        if (tenantResult.IsError)
            return tenantResult.Errors;
        TenantEntity tenant = tenantResult.Value;

        string rawToken = tokenGenerator.GenerateToken();
        string tokenHash = tokenGenerator.HashToken(rawToken);

        TenantInvitationEntity invitation = new()
        {
            Id = Guid.NewGuid(),
            Email = command.Request.Email.Trim(),
            NormalizedEmail = normalizedEmail,
            TokenHash = tokenHash,
            ExpiresAtUtc = timeProvider
                .GetUtcNow()
                .UtcDateTime.AddHours(emailOpts.InvitationTokenExpiryHours),
        };

        await invitationRepository.AddAsync(invitation, ct);
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
        return invitation.ToResponse();
    }
}
