using Identity.Entities;
using Identity.Enums;
using Identity.Features.TenantInvitation.Specifications;
using Identity.Features.User;
using Identity.Interfaces;
using Identity.Logging;
using Identity.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Identity.Security.Tenant;

/// <summary>
///     Resolves local <see cref="AppUser" /> for a Keycloak identity: links admin-created users,
///     provisions from accepted invitations, or denies with invitation-state-specific messages.
/// </summary>
public sealed class UserProvisioningService : IUserProvisioningService
{
    private readonly ITenantInvitationRepository _invitationRepository;
    private readonly ILogger<UserProvisioningService> _logger;
    private readonly TimeProvider _timeProvider;
    private readonly IUnitOfWork<IdentityDbMarker> _unitOfWork;
    private readonly IUserRepository _userRepository;

    public UserProvisioningService(
        IUserRepository userRepository,
        ITenantInvitationRepository invitationRepository,
        IUnitOfWork<IdentityDbMarker> unitOfWork,
        TimeProvider timeProvider,
        ILogger<UserProvisioningService> logger
    )
    {
        _userRepository = userRepository;
        _invitationRepository = invitationRepository;
        _unitOfWork = unitOfWork;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<UserAccessResolution> ResolveAppUserAccessAsync(
        string keycloakUserId,
        string email,
        string username,
        CancellationToken ct = default
    )
    {
        var byKeycloakId = new UserByKeycloakUserIdSpecification(keycloakUserId);

        AppUser? existing = await _userRepository.FirstOrDefaultAsync(byKeycloakId, ct);

        if (existing is not null)
        {
            _logger.UserProvisioningSkippedAlreadyExists(keycloakUserId);
            return UserAccessResolution.Allowed(existing);
        }

        string normalizedEmail = Email.NormalizeRaw(email);

        AppUser? unlinked = await _userRepository.FirstOrDefaultAsync(
            new UserUnlinkedByNormalizedEmailSpecification(normalizedEmail),
            ct
        );

        if (unlinked is not null)
        {
            unlinked.LinkKeycloak(keycloakUserId);
            await _userRepository.UpdateAsync(unlinked, ct);
            await _unitOfWork.CommitAsync(ct);
            _logger.UserProvisioningLinkedAdminCreatedUser(unlinked.Id, keycloakUserId);
            return UserAccessResolution.Allowed(unlinked);
        }

        TenantInvitation? acceptedInvitation = await _invitationRepository.FirstOrDefaultAsync(
            new AcceptedInvitationByNormalizedEmailSpecification(normalizedEmail),
            ct
        );

        if (acceptedInvitation is not null)
        {
            return await TryCreateUserFromAcceptedInvitationAsync(
                acceptedInvitation,
                keycloakUserId,
                email,
                username,
                ct
            );
        }

        TenantInvitation? latest = await _invitationRepository.FirstOrDefaultAsync(
            new LatestInvitationByNormalizedEmailSpecification(normalizedEmail),
            ct
        );

        if (latest is null)
        {
            return DenyAndLog(
                UserAccessErrorCodes.NoInvitation,
                UserAccessDeniedMessages.NoInvitation,
                normalizedEmail
            );
        }

        return latest.Status switch
        {
            InvitationStatus.Accepted => await TryCreateUserFromAcceptedInvitationAsync(
                latest,
                keycloakUserId,
                email,
                username,
                ct
            ),

            InvitationStatus.Pending when latest.IsExpired(_timeProvider) => DenyAndLog(
                UserAccessErrorCodes.InvitationExpired,
                UserAccessDeniedMessages.InvitationExpired,
                normalizedEmail
            ),

            InvitationStatus.Pending => DenyAndLog(
                UserAccessErrorCodes.PendingInvitation,
                UserAccessDeniedMessages.PendingInvitation,
                normalizedEmail
            ),

            InvitationStatus.Revoked => DenyAndLog(
                UserAccessErrorCodes.InvitationRevoked,
                UserAccessDeniedMessages.InvitationRevoked,
                normalizedEmail
            ),

            InvitationStatus.Expired => DenyAndLog(
                UserAccessErrorCodes.InvitationExpired,
                UserAccessDeniedMessages.InvitationExpired,
                normalizedEmail
            ),

            _ => DenyAndLog(
                UserAccessErrorCodes.NoInvitation,
                UserAccessDeniedMessages.NoInvitation,
                normalizedEmail
            ),
        };
    }

    private UserAccessResolution DenyAndLog(string code, string message, string normalizedEmail)
    {
        _logger.UserAccessDenied(code, normalizedEmail);
        return UserAccessResolution.Denied(code, message);
    }

    private async Task<UserAccessResolution> TryCreateUserFromAcceptedInvitationAsync(
        TenantInvitation invitation,
        string keycloakUserId,
        string email,
        string username,
        CancellationToken ct
    )
    {
        AppUser user = AppUser.Create(
            username,
            Email.FromPersistence(email),
            keycloakUserId,
            tenantId: invitation.TenantId
        );

        try
        {
            await _userRepository.AddAsync(user, ct);
            await _unitOfWork.CommitAsync(ct);
            _logger.UserProvisioned(user.Id, keycloakUserId, invitation.TenantId);
            return UserAccessResolution.Allowed(user);
        }
        catch (DbUpdateException ex)
        {
            _logger.UserProvisioningConcurrencyRetry(ex, keycloakUserId);

            AppUser? winner = await _userRepository.FirstOrDefaultAsync(
                new UserByKeycloakUserIdSpecification(keycloakUserId),
                ct
            );

            if (winner is not null)
                return UserAccessResolution.Allowed(winner);

            throw new InvalidOperationException(
                $"Provisioning failed for KeycloakUserId={keycloakUserId} and no existing user was found.",
                ex
            );
        }
    }
}
