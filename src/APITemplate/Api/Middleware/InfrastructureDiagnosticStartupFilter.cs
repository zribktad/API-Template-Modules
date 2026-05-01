using FileStorage.Contracts;
using Identity.Auth.Options;
using Npgsql;
using SharedKernel.Infrastructure.Configuration;

namespace APITemplate.Api.Middleware;

/// <summary>
///     Provides a startup filter to perform infrastructure diagnostics and logging
///     once the application container is fully built.
/// </summary>
internal sealed class InfrastructureDiagnosticStartupFilter(
    IConfiguration configuration,
    IWebHostEnvironment environment,
    ILogger<InfrastructureDiagnosticStartupFilter> logger
) : IStartupFilter
{
    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next)
    {
        return app =>
        {
            LogEnvironmentStatus();
            LogDatabaseStatus();
            LogRedisStatus();
            LogKeycloakStatus();
            LogFileStorageStatus();
            LogSecurityStatus();

            next(app);
        };
    }

    private void LogEnvironmentStatus()
    {
        logger.ApplicationStarting(environment.EnvironmentName);
    }

    private void LogDatabaseStatus()
    {
        string? connectionString = configuration.GetConnectionString("DefaultConnection");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            logger.DatabaseNotConfigured();
            return;
        }

        try
        {
            NpgsqlConnectionStringBuilder builder = new(connectionString);
            logger.PrimaryDatabaseInfo(
                builder.Host ?? string.Empty,
                builder.Port,
                builder.Database ?? string.Empty,
                builder.SslMode.ToString()
            );
        }
        catch (Exception ex)
        {
            logger.DatabaseParseFailed(ex);
        }
    }

    private void LogRedisStatus()
    {
        if (!configuration.IsRedisConfigured())
        {
            logger.RedisNotConfiguredFallback();
        }
        else
        {
            logger.RedisActive();
        }
    }

    private void LogKeycloakStatus()
    {
        KeycloakOptions? options = configuration.GetSection("Keycloak").Get<KeycloakOptions>();
        if (options is not null)
        {
            logger.IdentityProviderInfo(options.AuthServerUrl, options.Realm);
        }
    }

    private void LogFileStorageStatus()
    {
        FileStorageOptions? options = configuration
            .GetSection("FileStorage")
            .Get<FileStorageOptions>();
        if (options is not null)
        {
            logger.FileStorageInfo(
                options.BackendKey,
                options.MaxFileSizeBytes / (1024 * 1024),
                options.StagingTtlMinutes
            );
        }
    }

    private void LogSecurityStatus()
    {
        if (!environment.IsDevelopment())
        {
            logger.HstsEnabled();
        }
        else
        {
            logger.HstsDisabled();
        }
    }
}
