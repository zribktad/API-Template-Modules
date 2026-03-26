using APITemplate.Application.Common.Context;
using APITemplate.Application.Common.Email;
using APITemplate.Application.Common.Errors;
using APITemplate.Application.Common.Events;
using APITemplate.Application.Common.Extensions;
using APITemplate.Application.Common.Options;
using APITemplate.Application.Features.TenantInvitation.DTOs;
using APITemplate.Application.Features.TenantInvitation.Mappings;
using APITemplate.Domain.Entities;
using APITemplate.Domain.Interfaces;
using ErrorOr;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Wolverine;
using TenantInvitationEntity = APITemplate.Domain.Entities.TenantInvitation;

namespace APITemplate.Application.Features.TenantInvitation;

public sealed record CreateTenantInvitationCommand(CreateTenantInvitationRequest Request);

public sealed class CreateTenantInvitationCommandHandler
{
    public static async Task<ErrorOr<TenantInvitationResponse>> HandleAsync(
        CreateTenantInvitationCommand command,
        ITenantInvitationRepository invitationRepository,
        ITenantRepository tenantRepository,
        IUnitOfWork unitOfWork,
        ISecureTokenGenerator tokenGenerator,
        IMessageBus bus,
        ITenantProvider tenantProvider,
        TimeProvider timeProvider,
        IOptions<EmailOptions> emailOptions,
        ILogger<CreateTenantInvitationCommandHandler> logger,
        CancellationToken ct
    )
    {
        var emailOpts = emailOptions.Value;
        var normalizedEmail = AppUser.NormalizeEmail(command.Request.Email);

        if (await invitationRepository.HasPendingInvitationAsync(normalizedEmail, ct))
            return DomainErrors.Invitations.AlreadyPending(command.Request.Email);

        var tenantResult = await tenantRepository.GetByIdOrError(
            tenantProvider.TenantId,
            DomainErrors.Tenants.NotFound(tenantProvider.TenantId),
            ct
        );
        if (tenantResult.IsError)
            return tenantResult.Errors;
        var tenant = tenantResult.Value;

        var rawToken = tokenGenerator.GenerateToken();
        var tokenHash = tokenGenerator.HashToken(rawToken);

        var invitation = new TenantInvitationEntity
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
