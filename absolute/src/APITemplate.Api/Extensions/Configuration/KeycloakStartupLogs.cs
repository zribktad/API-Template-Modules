namespace APITemplate.Api.Extensions.Configuration;

/// <summary>
/// Source-generated high-performance logger methods for Keycloak readiness-check events
/// emitted during application startup.
/// </summary>
internal static partial class KeycloakStartupLogs
{
    [LoggerMessage(
        EventId = 3001,
        Level = LogLevel.Warning,
        Message = "Keycloak configuration is missing, skipping readiness check"
    )]
    public static partial void KeycloakConfigMissing(this ILogger logger);

    [LoggerMessage(
        EventId = 3002,
        Level = LogLevel.Information,
        Message = "Keycloak readiness check skipped via configuration"
    )]
    public static partial void KeycloakReadinessCheckSkipped(this ILogger logger);

    [LoggerMessage(
        EventId = 3003,
        Level = LogLevel.Information,
        Message = "Keycloak is ready at {Url}"
    )]
    public static partial void KeycloakReady(this ILogger logger, string url);

    [LoggerMessage(
        EventId = 3004,
        Level = LogLevel.Warning,
        Message = "Keycloak not ready, retrying ({Attempt}/{MaxRetries})..."
    )]
    public static partial void KeycloakRetrying(this ILogger logger, int attempt, int maxRetries);

    [LoggerMessage(
        EventId = 3005,
        Level = LogLevel.Error,
        Message = "Keycloak did not become available after {MaxRetries} retries"
    )]
    public static partial void KeycloakUnavailable(
        this ILogger logger,
        Exception exception,
        int maxRetries
    );
}
