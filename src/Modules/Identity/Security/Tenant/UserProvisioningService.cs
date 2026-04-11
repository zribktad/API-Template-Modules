using Identity.Features.TenantInvitation.Specifications;
using Identity.Logging;
using Identity.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Identity.Security.Tenant;

/// <summary>
///     Provisions a new <see cref="AppUser" /> on first login when an accepted
///     <see cref="TenantInvitation" /> exists for the authenticated email address.
///     Idempotent: returns the existing user immediately if one is already linked
///     to the given Keycloak subject ID.
/// </summary>
public sealed class UserProvisioningService : IUserProvisioningService
{
    private readonly ITenantInvitationRepository _invitationRepository;
    private readonly ILogger<UserProvisioningService> _logger;
    private readonly IUnitOfWork<IdentityDbMarker> _unitOfWork;
    private readonly IUserRepository _userRepository;

    public UserProvisioningService(
        IUserRepository userRepository,
        ITenantInvitationRepository invitationRepository,
        IUnitOfWork<IdentityDbMarker> unitOfWork,
        ILogger<UserProvisioningService> logger
    )
    {
        _userRepository = userRepository;
        _invitationRepository = invitationRepository;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<AppUser?> ProvisionIfNeededAsync(
        string keycloakUserId,
        string email,
        string username,
        CancellationToken ct = default
    )
    {
        // 1. Check if the user is already provisioned — bypass tenant filter because
        //    we only have the Keycloak subject ID, not a tenant context yet.
        AppUser? existing = await _userRepository.FirstOrDefaultAsync(
            new UserByKeycloakUserIdSpecification(keycloakUserId),
            ct
        );

        if (existing is not null)
        {
            _logger.UserProvisioningSkippedAlreadyExists(keycloakUserId);
            return existing;
        }

        // 2. Look for an accepted invitation matching the normalised email.
        //    Bypass tenant filter — at this point no tenant context is active.
        string normalizedEmail = Email.NormalizeRaw(email);

        TenantInvitation? invitation = await _invitationRepository.FirstOrDefaultAsync(
            new AcceptedInvitationByNormalizedEmailSpecification(normalizedEmail),
            ct
        );

        if (invitation is null)
        {
            _logger.UserProvisioningSkippedNoInvitation(normalizedEmail);
            return null;
        }

        // 3. Provision a new user from the invitation data.
        // The email comes from Keycloak (trusted identity provider), so no re-validation is needed.
        // TenantId must be set explicitly here. During OnTokenValidated, no tenant context
        // is active (ITenantProvider.HasTenant == false), so AuditableEntityStateManager
        // will NOT auto-assign TenantId from the tenant provider. This explicit assignment
        // is load-bearing and must not be removed.
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
            return user;
        }
        catch (DbUpdateException ex)
        {
            // Concurrent request may have provisioned this user — re-fetch the winner.
            _logger.UserProvisioningConcurrencyRetry(ex, keycloakUserId);

            return await _userRepository.FirstOrDefaultAsync(
                    new UserByKeycloakUserIdSpecification(keycloakUserId),
                    ct
                )
                ?? throw new InvalidOperationException(
                    $"Provisioning failed for KeycloakUserId={keycloakUserId} and no existing user was found.",
                    ex
                );
        }
    }
}
