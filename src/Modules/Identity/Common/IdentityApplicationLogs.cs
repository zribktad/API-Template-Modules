using Microsoft.Extensions.Logging;
using SharedKernel.Infrastructure.Logging;

namespace Identity.Logging;

/// <summary>
/// Source-generated logger extension methods for Identity application diagnostics.
/// </summary>
internal static partial class IdentityApplicationLogs
{
    // CreateUserCommandHandler (2001-2002)
    [LoggerMessage(
        EventId = 2001,
        Level = LogLevel.Error,
        Message = "DB save failed after creating Keycloak user {KeycloakUserId}. Attempting compensating delete."
    )]
    public static partial void CreateUserDbSaveFailed(
        this ILogger logger,
        Exception exception,
        [SensitiveData] string keycloakUserId
    );

    [LoggerMessage(
        EventId = 2002,
        Level = LogLevel.Error,
        Message = "Compensating Keycloak delete failed for user {KeycloakUserId}. Manual cleanup required."
    )]
    public static partial void CreateUserCompensatingDeleteFailed(
        this ILogger logger,
        Exception exception,
        [SensitiveData] string keycloakUserId
    );

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
}

