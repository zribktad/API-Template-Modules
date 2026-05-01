using Microsoft.Extensions.Configuration;
using SharedKernel.Application.Options.Infrastructure;
using StackExchange.Redis;

namespace SharedKernel.Infrastructure.Configuration;

/// <summary>
///     Helpers for Redis connection configuration (StackExchange.Redis).
/// </summary>
public static class RedisConfigurationExtensions
{
    /// <summary>
    ///     Returns whether a non-empty <see cref="RedisOptions.ConnectionString" /> is configured.
    ///     Used to register distributed (Redis) vs. in-process-only infrastructure.
    /// </summary>
    public static bool IsRedisConfigured(this IConfiguration configuration)
    {
        string? connectionString = configuration
            .SectionFor<RedisOptions>()
            .GetValue<string>(nameof(RedisOptions.ConnectionString));

        return !string.IsNullOrWhiteSpace(connectionString);
    }

    /// <summary>
    ///     Builds the StackExchange.Redis connection settings shared by distributed cache,
    ///     output cache, and IConnectionMultiplexer.
    /// </summary>
    /// <param name="configuration">The application configuration.</param>
    /// <returns>The parsed and configured ConfigurationOptions.</returns>
    public static ConfigurationOptions BuildRedisConfigurationOptions(
        this IConfiguration configuration
    )
    {
        RedisOptions redisOptions =
            configuration.SectionFor<RedisOptions>().Get<RedisOptions>() ?? new RedisOptions();

        if (string.IsNullOrWhiteSpace(redisOptions.ConnectionString))
        {
            // Return a default configuration to avoid null-refs,
            // but effectively it won't be used for connection if IsRedisConfigured() is used as a guard.
            return new ConfigurationOptions { AbortOnConnectFail = false };
        }

        ConfigurationOptions redisConfig = ConfigurationOptions.Parse(
            redisOptions.ConnectionString
        );

        redisConfig.ConnectTimeout = redisOptions.ConnectTimeoutMs;
        redisConfig.SyncTimeout = redisOptions.SyncTimeoutMs;
        redisConfig.AbortOnConnectFail = false;

        return redisConfig;
    }
}
