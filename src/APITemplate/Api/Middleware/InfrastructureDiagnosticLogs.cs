namespace APITemplate.Api.Middleware;

/// <summary>
///     Source-generated logger extension methods for infrastructure diagnostics.
/// </summary>
internal static partial class InfrastructureDiagnosticLogs
{
    [LoggerMessage(
        EventId = 2001,
        Level = LogLevel.Information,
        Message = "Application is starting in {EnvironmentName} environment."
    )]
    public static partial void ApplicationStarting(this ILogger logger, string environmentName);

    [LoggerMessage(
        EventId = 2002,
        Level = LogLevel.Information,
        Message = "Primary database: {Host}:{Port} / {Database} (SSL: {SslMode})"
    )]
    public static partial void PrimaryDatabaseInfo(
        this ILogger logger,
        string host,
        int port,
        string database,
        string sslMode
    );

    [LoggerMessage(
        EventId = 2003,
        Level = LogLevel.Warning,
        Message = "Primary database connection string 'DefaultConnection' is not configured."
    )]
    public static partial void DatabaseNotConfigured(this ILogger logger);

    [LoggerMessage(
        EventId = 2004,
        Level = LogLevel.Warning,
        Message = "Failed to parse primary database connection string for diagnostics."
    )]
    public static partial void DatabaseParseFailed(this ILogger logger, Exception exception);

    [LoggerMessage(
        EventId = 2005,
        Level = LogLevel.Information,
        Message = "Redis is not configured. Falling back to in-memory distributed cache and local Data Protection storage."
    )]
    public static partial void RedisNotConfiguredFallback(this ILogger logger);

    [LoggerMessage(
        EventId = 2006,
        Level = LogLevel.Debug,
        Message = "Redis infrastructure is configured and active."
    )]
    public static partial void RedisActive(this ILogger logger);

    [LoggerMessage(
        EventId = 2007,
        Level = LogLevel.Information,
        Message = "Identity Provider: Keycloak (Server: {Url}, Realm: {Realm})"
    )]
    public static partial void IdentityProviderInfo(this ILogger logger, string url, string realm);

    [LoggerMessage(
        EventId = 2008,
        Level = LogLevel.Information,
        Message = "File Storage: {Backend} (Max size: {MaxSizeMB} MB, Staging TTL: {Ttl} min)"
    )]
    public static partial void FileStorageInfo(
        this ILogger logger,
        string backend,
        long maxSizeMB,
        int ttl
    );

    [LoggerMessage(
        EventId = 2009,
        Level = LogLevel.Information,
        Message = "Security: HSTS is enabled for non-Development environments."
    )]
    public static partial void HstsEnabled(this ILogger logger);

    [LoggerMessage(
        EventId = 2010,
        Level = LogLevel.Debug,
        Message = "Security: HSTS is disabled in Development environment."
    )]
    public static partial void HstsDisabled(this ILogger logger);
}
