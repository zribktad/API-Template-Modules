using APITemplate.Application.Common.Security;
using APITemplate.Domain.Entities;
using APITemplate.Domain.Enums;
using APITemplate.Domain.Interfaces;
using APITemplate.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace APITemplate.Infrastructure.Security.Tenant;

/// <summary>
/// Provisions a new <see cref="AppUser"/> on first login when an accepted
/// <see cref="TenantInvitation"/> exists for the authenticated email address.
/// Idempotent: returns the existing user immediately if one is already linked
/// to the given Keycloak subject ID.
/// </summary>
public sealed class UserProvisioningService : IUserProvisioningService
{
    // AppDbContext is injected directly (rather than via repository interfaces) because:
    // 1. IgnoreQueryFilters() is required — no tenant context exists during OnTokenValidated
    // 2. Both reads use global filter bypass; routing through repositories would require
    //    adding filter-bypass methods to the repository interfaces for a single use case
    private readonly AppDbContext _db;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<UserProvisioningService> _logger;

    public UserProvisioningService(
        AppDbContext db,
        IUnitOfWork unitOfWork,
        ILogger<UserProvisioningService> logger)
    {
        _db = db;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<AppUser?> ProvisionIfNeededAsync(
        string keycloakUserId,
        string email,
        string username,
        CancellationToken ct = default)
    {
        // 1. Check if the user is already provisioned — bypass tenant filter because
        //    we only have the Keycloak subject ID, not a tenant context yet.
        var existing = await _db.Users
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(u => u.KeycloakUserId == keycloakUserId, ct);

        if (existing is not null)
        {
            _logger.LogDebug(
                "User provisioning skipped — AppUser already exists for KeycloakUserId={KeycloakUserId}",
                keycloakUserId);
            return existing;
        }

        // 2. Look for an accepted invitation matching the normalised email.
        //    Bypass tenant filter — at this point no tenant context is active.
        var normalizedEmail = AppUser.NormalizeEmail(email);

        var invitation = await _db.TenantInvitations
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(
                i => i.NormalizedEmail == normalizedEmail && i.Status == InvitationStatus.Accepted,
                ct);

        if (invitation is null)
        {
            _logger.LogInformation(
                "User provisioning skipped — no accepted invitation found for email={NormalizedEmail}",
                normalizedEmail);
            return null;
        }

        // 3. Provision a new user from the invitation data.
        var user = new AppUser
        {
            Username = username,
            Email = email,
            KeycloakUserId = keycloakUserId,
            // TenantId must be set explicitly here. During OnTokenValidated, no tenant context
            // is active (ITenantProvider.HasTenant == false), so AuditableEntityStateManager
            // will NOT auto-assign TenantId from the tenant provider. This explicit assignment
            // is load-bearing and must not be removed.
            TenantId = invitation.TenantId,
            IsActive = true,
            Role = UserRole.User,
        };

        try
        {
            await _db.Users.AddAsync(user, ct);
            await _unitOfWork.CommitAsync(ct);
            _logger.LogInformation(
                "Provisioned new AppUser={UserId} for KeycloakUserId={KeycloakUserId}, TenantId={TenantId}",
                user.Id,
                keycloakUserId,
                invitation.TenantId);
            return user;
        }
        catch (DbUpdateException ex)
        {
            // Concurrent request may have provisioned this user — re-fetch the winner.
            _logger.LogWarning(ex, "DbUpdateException during provisioning for {KeycloakUserId}. Re-fetching.", keycloakUserId);

            return await _db.Users.IgnoreQueryFilters()
                .FirstOrDefaultAsync(u => u.KeycloakUserId == keycloakUserId, ct)
                ?? throw new InvalidOperationException(
                    $"Provisioning failed for KeycloakUserId={keycloakUserId} and no existing user was found.", ex);
        }
    }
}
