using Microsoft.Extensions.Logging;
using SharedKernel.Infrastructure.Logging;

namespace Identity.Infrastructure.Logging;

/// <summary>
/// Source-generated logger extension methods for Identity infrastructure diagnostics.
/// </summary>
internal static partial class IdentityInfrastructureLogs
{
    // KeycloakAdminService (3001-3006)
    [LoggerMessage(
        EventId = 3001,
        Level = LogLevel.Information,
        Message = "Created Keycloak user {Username} with id {KeycloakUserId}"
    )]
    public static partial void KeycloakUserCreated(
        this ILogger logger,
        [PersonalData] string username,
        string keycloakUserId
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
