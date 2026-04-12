using Microsoft.Extensions.Configuration;
using SharedKernel.Application.Options.Infrastructure;

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
}
