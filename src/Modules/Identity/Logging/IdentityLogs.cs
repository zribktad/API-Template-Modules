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

    // UserProvisioningService (3020-3023)
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

    // CleanupExpiredInvitationsHandler (3030)
    [LoggerMessage(
        EventId = 3030,
        Level = LogLevel.Information,
        Message = "Cleaned up {Count} expired invitations."
    )]
    public static partial void ExpiredInvitationsCleanedUp(this ILogger logger, int count);
}
