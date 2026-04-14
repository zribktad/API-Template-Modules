using Microsoft.Extensions.Logging;
using SharedKernel.Infrastructure.Logging;

namespace Identity.Logging;

/// <summary>
///     Source-generated logger extension methods for Identity diagnostics.
/// </summary>
internal static partial class IdentityLogs
{
    // ProvisionKeycloakUserHandler (2001-2003)
    [LoggerMessage(
        EventId = 2003,
        Level = LogLevel.Error,
        Message = "Keycloak provisioning permanently failed for AppUser {UserId}. User has ProvisioningStatus=Pending and cannot log in. Check dead-letter queue."
    )]
    public static partial void KeycloakProvisioningPermanentlyFailed(
        this ILogger logger,
        Guid userId
    );

    [LoggerMessage(
        EventId = 2001,
        Level = LogLevel.Information,
        Message = "Provisioned Keycloak account for AppUser {UserId} — KeycloakUserId={KeycloakUserId}"
    )]
    public static partial void KeycloakUserProvisioned(
        this ILogger logger,
        Guid userId,
        [SensitiveData] string keycloakUserId
    );

    [LoggerMessage(
        EventId = 2002,
        Level = LogLevel.Warning,
        Message = "ProvisionKeycloakUserEvent for AppUser {UserId} skipped — user not found or already provisioned."
    )]
    public static partial void ProvisionKeycloakUserSkipped(this ILogger logger, Guid userId);

    // DeleteUserCommandHandler (2010)
    [LoggerMessage(
        EventId = 2010,
        Level = LogLevel.Critical,
        Message = "DB delete failed after Keycloak user {KeycloakUserId} was already deleted. Manual cleanup required."
    )]
    public static partial void DeleteUserDbDeleteFailed(
        this ILogger logger,
        Exception exception,
        [SensitiveData] string? keycloakUserId
    );

    // KeycloakPasswordResetCommandHandler (2020)
    [LoggerMessage(
        EventId = 2020,
        Level = LogLevel.Warning,
        Message = "Failed to send password reset email for user {UserId}."
    )]
    public static partial void PasswordResetEmailFailed(
        this ILogger logger,
        Exception exception,
        Guid userId
    );

    // KeycloakAdminService (3001-3006)
    [LoggerMessage(
        EventId = 3001,
        Level = LogLevel.Information,
        Message = "Created Keycloak user {Username} with id {KeycloakUserId}"
    )]
    public static partial void KeycloakUserCreated(
        this ILogger logger,
        [PersonalData] string username,
        [SensitiveData] string keycloakUserId
    );

    [LoggerMessage(
        EventId = 3007,
        Level = LogLevel.Information,
        Message = "Keycloak user already exists for {Username}, resolved existing id {KeycloakUserId}"
    )]
    public static partial void KeycloakUserAlreadyExistsResolved(
        this ILogger logger,
        [PersonalData] string username,
        [SensitiveData] string keycloakUserId
    );

    [LoggerMessage(
        EventId = 3002,
        Level = LogLevel.Warning,
        Message = "Failed to send setup email for Keycloak user {KeycloakUserId}. User was created but has no setup email."
    )]
    public static partial void KeycloakSetupEmailFailed(
        this ILogger logger,
        Exception exception,
        string keycloakUserId
    );

    [LoggerMessage(
        EventId = 3003,
        Level = LogLevel.Information,
        Message = "Sent password reset email to Keycloak user {KeycloakUserId}"
    )]
    public static partial void KeycloakPasswordResetEmailSent(
        this ILogger logger,
        string keycloakUserId
    );

    [LoggerMessage(
        EventId = 3004,
        Level = LogLevel.Information,
        Message = "Set Keycloak user {KeycloakUserId} enabled={Enabled}"
    )]
    public static partial void KeycloakUserEnabledSet(
        this ILogger logger,
        string keycloakUserId,
        bool enabled
    );

    [LoggerMessage(
        EventId = 3005,
        Level = LogLevel.Warning,
        Message = "Keycloak user {KeycloakUserId} was not found during delete — treating as already deleted."
    )]
    public static partial void KeycloakUserNotFoundOnDelete(
        this ILogger logger,
        string keycloakUserId
    );

    [LoggerMessage(
        EventId = 3006,
        Level = LogLevel.Information,
        Message = "Deleted Keycloak user {KeycloakUserId}"
    )]
    public static partial void KeycloakUserDeleted(this ILogger logger, string keycloakUserId);

    [LoggerMessage(
        EventId = 3008,
        Level = LogLevel.Information,
        Message = "Set password for Keycloak user {KeycloakUserId}, temporary={Temporary}"
    )]
    public static partial void KeycloakUserPasswordSet(
        this ILogger logger,
        [SensitiveData] string keycloakUserId,
        bool temporary
    );

    [LoggerMessage(
        EventId = 3009,
        Level = LogLevel.Information,
        Message = "Logged out all Keycloak sessions for user {KeycloakUserId}"
    )]
    public static partial void KeycloakUserAllSessionsLoggedOut(
        this ILogger logger,
        [SensitiveData] string keycloakUserId
    );

    [LoggerMessage(
        EventId = 3011,
        Level = LogLevel.Warning,
        Message = "Logout-all-sessions: Keycloak user {KeycloakUserId} not found (404) — treating as no-op"
    )]
    public static partial void KeycloakUserLogoutAllSessionsUserNotFound(
        this ILogger logger,
        [SensitiveData] string keycloakUserId
    );

    [LoggerMessage(
        EventId = 3013,
        Level = LogLevel.Warning,
        Message = "Logout all Keycloak sessions failed for user {KeycloakUserId}; continuing with local BFF session revocation."
    )]
    public static partial void KeycloakLogoutAllSessionsFailed(
        this ILogger logger,
        Exception exception,
        [SensitiveData] string keycloakUserId
    );

    // KeycloakAdminTokenProvider (3010)
    [LoggerMessage(
        EventId = 3010,
        Level = LogLevel.Error,
        Message = "Failed to acquire Keycloak admin token. Status: {Status}. Body: {Body}"
    )]
    public static partial void KeycloakAdminTokenAcquisitionFailed(
        this ILogger logger,
        int status,
        [SensitiveData] string body
    );

    // ResolveAppUserAccessHandler (3020-3023)
    [LoggerMessage(
        EventId = 3020,
        Level = LogLevel.Debug,
        Message = "User provisioning skipped — AppUser already exists for KeycloakUserId={KeycloakUserId}"
    )]
    public static partial void UserProvisioningSkippedAlreadyExists(
        this ILogger logger,
        string keycloakUserId
    );

    [LoggerMessage(
        EventId = 3021,
        Level = LogLevel.Information,
        Message = "User provisioning skipped — no accepted invitation found for email={NormalizedEmail}"
    )]
    public static partial void UserProvisioningSkippedNoInvitation(
        this ILogger logger,
        [PersonalData] string normalizedEmail
    );

    [LoggerMessage(
        EventId = 3022,
        Level = LogLevel.Information,
        Message = "Provisioned new AppUser={UserId} for KeycloakUserId={KeycloakUserId}, TenantId={TenantId}"
    )]
    public static partial void UserProvisioned(
        this ILogger logger,
        Guid userId,
        string keycloakUserId,
        Guid tenantId
    );

    [LoggerMessage(
        EventId = 3023,
        Level = LogLevel.Warning,
        Message = "DbUpdateException during provisioning for {KeycloakUserId}. Re-fetching."
    )]
    public static partial void UserProvisioningConcurrencyRetry(
        this ILogger logger,
        Exception exception,
        string keycloakUserId
    );

    [LoggerMessage(
        EventId = 3024,
        Level = LogLevel.Warning,
        Message = "Access denied during provisioning: {ErrorCode} for normalised email {NormalizedEmail}."
    )]
    public static partial void UserAccessDenied(
        this ILogger logger,
        string errorCode,
        [PersonalData] string normalizedEmail
    );

    [LoggerMessage(
        EventId = 3025,
        Level = LogLevel.Information,
        Message = "Linked Keycloak id {KeycloakUserId} to AppUser {UserId} (admin-created, previously unlinked)."
    )]
    public static partial void UserProvisioningLinkedAdminCreatedUser(
        this ILogger logger,
        Guid userId,
        [SensitiveData] string keycloakUserId
    );

    // CleanupExpiredInvitationsHandler (3030)
    [LoggerMessage(
        EventId = 3030,
        Level = LogLevel.Information,
        Message = "Cleaned up {Count} expired invitations."
    )]
    public static partial void ExpiredInvitationsCleanedUp(this ILogger logger, int count);

    // CleanupExpiredBffSessionsHandler (3031)
    [LoggerMessage(
        EventId = 3031,
        Level = LogLevel.Information,
        Message = "Cleaned up {Count} expired BFF sessions."
    )]
    public static partial void ExpiredBffSessionsCleanedUp(this ILogger logger, int count);

    // IdentityTokenValidatedPipeline / CookieSessionRefresher (3040-3045)
    [LoggerMessage(
        EventId = 3040,
        Level = LogLevel.Warning,
        Message = "Missing required {ClaimType} claim on {Scheme} token."
    )]
    public static partial void MissingRequiredTenantClaimOnToken(
        this ILogger logger,
        string claimType,
        string scheme
    );

    [LoggerMessage(
        EventId = 3041,
        Level = LogLevel.Warning,
        Message = "User provisioning failed during token validation."
    )]
    public static partial void UserProvisioningFailedDuringTokenValidation(
        this ILogger logger,
        Exception exception
    );

    [LoggerMessage(
        EventId = 3042,
        Level = LogLevel.Warning,
        Message = "BFF session refresh failed; rejecting principal."
    )]
    public static partial void BffSessionRefreshFailedRejectingPrincipal(this ILogger logger);

    [LoggerMessage(
        EventId = 3043,
        Level = LogLevel.Warning,
        Message = "BFF session refresh skipped; no refresh token found."
    )]
    public static partial void BffSessionRefreshSkippedNoRefreshToken(this ILogger logger);

    [LoggerMessage(
        EventId = 3044,
        Level = LogLevel.Warning,
        Message = "Keycloak token endpoint returned {StatusCode} during BFF refresh."
    )]
    public static partial void KeycloakTokenEndpointReturnedNonSuccessDuringRefresh(
        this ILogger logger,
        int statusCode
    );

    [LoggerMessage(
        EventId = 3045,
        Level = LogLevel.Warning,
        Message = "Token refresh failed; rejecting principal."
    )]
    public static partial void TokenRefreshFailedRejectingPrincipal(
        this ILogger logger,
        Exception exception
    );

    [LoggerMessage(
        EventId = 3051,
        Level = LogLevel.Debug,
        Message = "User access allowed cache {Operation} failed; continuing without cache."
    )]
    public static partial void UserAccessAllowedCacheOperationFailed(
        this ILogger logger,
        string operation,
        Exception exception
    );

    [LoggerMessage(
        EventId = 3046,
        Level = LogLevel.Warning,
        Message = "BFF session refresh skipped; expires_at token value is missing or invalid. Rejecting principal."
    )]
    public static partial void BffSessionRefreshInvalidExpiresAtRejectingPrincipal(
        this ILogger logger
    );

    [LoggerMessage(
        EventId = 3047,
        Level = LogLevel.Warning,
        Message = "Keycloak refresh response was missing a valid access token or expiry. Rejecting principal."
    )]
    public static partial void KeycloakRefreshResponseInvalidRejectingPrincipal(
        this ILogger logger
    );

    // PostgresCachedBffSessionStore (3048-3049)
    [LoggerMessage(
        EventId = 3048,
        Level = LogLevel.Warning,
        Message = "Failed to unprotect session {SessionId} tokens — possible key rotation."
    )]
    public static partial void BffSessionUnprotectFailed(
        this ILogger logger,
        Exception exception,
        string sessionId
    );

    [LoggerMessage(
        EventId = 3049,
        Level = LogLevel.Warning,
        Message = "Malformed protected payload for session {SessionId}."
    )]
    public static partial void BffSessionPayloadMalformed(
        this ILogger logger,
        Exception exception,
        string sessionId
    );

    // BffSessionService (3050)
    [LoggerMessage(
        EventId = 3050,
        Level = LogLevel.Warning,
        Message = "Failed to mutate session {SessionId} after {MaxAttempts} optimistic concurrency attempts."
    )]
    public static partial void BffSessionMutationFailed(
        this ILogger logger,
        string sessionId,
        int maxAttempts
    );

    // RolePermissionsInvalidatedEventHandler / UserPermissionsInvalidatedEventHandler (3060-3061)
    [LoggerMessage(
        EventId = 3060,
        Level = LogLevel.Information,
        Message = "Invalidated permissions cache for {Count} users assigned to RoleId {RoleId}"
    )]
    public static partial void PermissionCacheInvalidatedForRole(
        this ILogger logger,
        int count,
        Guid roleId
    );

    [LoggerMessage(
        EventId = 3061,
        Level = LogLevel.Information,
        Message = "Invalidated permissions cache for AppUserId {AppUserId}"
    )]
    public static partial void PermissionCacheInvalidatedForUser(
        this ILogger logger,
        Guid appUserId
    );
}
