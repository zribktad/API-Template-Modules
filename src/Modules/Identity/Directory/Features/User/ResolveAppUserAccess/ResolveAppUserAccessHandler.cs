using Identity.Directory.Entities;
using Identity.Directory.Enums;
using TenantInvitationEntity = Identity.Directory.Entities.TenantInvitation;
using Identity.Directory.Features.TenantInvitation.Specifications;
using Identity.Directory.Interfaces;
using Identity.Logging;
using Identity.Directory.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Identity.Directory.Features.User;

/// <summary>
///     Resolves local <see cref="AppUser" /> for a Keycloak identity: links admin-created users,
///     provisions from accepted invitations, or denies with invitation-state-specific messages.
/// </summary>
public sealed class ResolveAppUserAccessHandler
{
    public static async Task<UserAccessResolution> HandleAsync(
        ResolveAppUserAccessQuery query,
        IUserRepository userRepository,
        ITenantInvitationRepository invitationRepository,
        IUnitOfWork<IdentityDbMarker> unitOfWork,
        TimeProvider timeProvider,
        ILogger<ResolveAppUserAccessHandler> logger,
        CancellationToken ct
    )
    {
        string keycloakUserId = query.KeycloakUserId;
        string email = query.Email;
        string username = query.Username;

        var byKeycloakId = new UserByKeycloakUserIdSpecification(keycloakUserId);

        AppUser? existing = await userRepository.FirstOrDefaultAsync(byKeycloakId, ct);

        if (existing is not null)
        {
            logger.UserProvisioningSkippedAlreadyExists(keycloakUserId);
            return UserAccessResolution.Allowed(existing);
        }

        string normalizedEmail = NormalizedString.Normalize(email);

        AppUser? unlinked = await userRepository.FirstOrDefaultAsync(
            new UserUnlinkedByNormalizedEmailSpecification(normalizedEmail),
            ct
        );

        if (unlinked is not null)
        {
            unlinked.LinkKeycloak(keycloakUserId);
            await userRepository.UpdateAsync(unlinked, ct);
            await unitOfWork.CommitAsync(ct);
            logger.UserProvisioningLinkedAdminCreatedUser(unlinked.Id, keycloakUserId);
            return UserAccessResolution.Allowed(unlinked);
        }

        TenantInvitationEntity? acceptedInvitation = await invitationRepository.FirstOrDefaultAsync(
            new AcceptedInvitationByNormalizedEmailSpecification(normalizedEmail),
            ct
        );

        if (acceptedInvitation is not null)
        {
            return await TryCreateUserFromAcceptedInvitationAsync(
                userRepository,
                unitOfWork,
                logger,
                acceptedInvitation,
                keycloakUserId,
                email,
                username,
                ct
            );
        }

        TenantInvitationEntity? latest = await invitationRepository.FirstOrDefaultAsync(
            new LatestInvitationByNormalizedEmailSpecification(normalizedEmail),
            ct
        );

        if (latest is null)
        {
            return DenyAndLog(
                logger,
                UserAccessErrorCodes.NoInvitation,
                UserAccessDeniedMessages.NoInvitation,
                normalizedEmail
            );
        }

        return latest.Status switch
        {
            InvitationStatus.Accepted => await TryCreateUserFromAcceptedInvitationAsync(
                userRepository,
                unitOfWork,
                logger,
                latest,
                keycloakUserId,
                email,
                username,
                ct
            ),

            InvitationStatus.Pending when latest.IsExpired(timeProvider) => DenyAndLog(
                logger,
                UserAccessErrorCodes.InvitationExpired,
                UserAccessDeniedMessages.InvitationExpired,
                normalizedEmail
            ),

            InvitationStatus.Pending => DenyAndLog(
                logger,
                UserAccessErrorCodes.PendingInvitation,
                UserAccessDeniedMessages.PendingInvitation,
                normalizedEmail
            ),

            InvitationStatus.Revoked => DenyAndLog(
                logger,
                UserAccessErrorCodes.InvitationRevoked,
                UserAccessDeniedMessages.InvitationRevoked,
                normalizedEmail
            ),

            InvitationStatus.Expired => DenyAndLog(
                logger,
                UserAccessErrorCodes.InvitationExpired,
                UserAccessDeniedMessages.InvitationExpired,
                normalizedEmail
            ),

            _ => DenyAndLog(
                logger,
                UserAccessErrorCodes.NoInvitation,
                UserAccessDeniedMessages.NoInvitation,
                normalizedEmail
            ),
        };
    }

    private static UserAccessResolution DenyAndLog(
        ILogger<ResolveAppUserAccessHandler> logger,
        string code,
        string message,
        string normalizedEmail
    )
    {
        logger.UserAccessDenied(code, normalizedEmail);
        return UserAccessResolution.Denied(code, message);
    }

    private static async Task<UserAccessResolution> TryCreateUserFromAcceptedInvitationAsync(
        IUserRepository userRepository,
        IUnitOfWork<IdentityDbMarker> unitOfWork,
        ILogger<ResolveAppUserAccessHandler> logger,
        TenantInvitationEntity invitation,
        string keycloakUserId,
        string email,
        string username,
        CancellationToken ct
    )
    {
        AppUser user = AppUser.Create(
            username,
            email,
            keycloakUserId,
            tenantId: invitation.TenantId
        );

        try
        {
            await userRepository.AddAsync(user, ct);
            await unitOfWork.CommitAsync(ct);
            logger.UserProvisioned(user.Id, keycloakUserId, invitation.TenantId);
            return UserAccessResolution.Allowed(user);
        }
        catch (DbUpdateException ex)
        {
            logger.UserProvisioningConcurrencyRetry(ex, keycloakUserId);

            AppUser? winner = await userRepository.FirstOrDefaultAsync(
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
