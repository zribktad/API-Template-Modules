using APITemplate.Api.Cache;
using Microsoft.AspNetCore.DataProtection;
using SharedKernel.Infrastructure.Configuration;
using StackExchange.Redis;

namespace APITemplate.Api.Extensions;

/// <summary>
///     Provides extension methods for configuring Redis infrastructure.
/// </summary>
public static class RedisServiceCollectionExtensions
{
    /// <summary>
    ///     Registers Redis multiplexer, distributed cache, and data protection keys persistence.
    ///     Falls back to distributed memory cache if Redis is not configured.
    /// </summary>
    /// <param name="services">The IServiceCollection to add services to.</param>
    /// <param name="configuration">The application configuration.</param>
    /// <param name="redisConfiguration">The parsed Redis configuration options, if available.</param>
    /// <returns>The updated IServiceCollection.</returns>
    public static IServiceCollection AddRedisInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration,
        ConfigurationOptions? redisConfiguration
    )
    {
        services.AddValidatedOptions<RedisOptions>(configuration);

        if (redisConfiguration is not null)
        {
            IConnectionMultiplexer redis = ConnectionMultiplexer.Connect(redisConfiguration);
            services.AddSingleton<IConnectionMultiplexer>(_ => redis);

            services.AddStackExchangeRedisCache(opts =>
            {
                opts.ConfigurationOptions = redisConfiguration;
            });

            services
                .AddDataProtection()
                .SetApplicationName("APITemplate")
                .PersistKeysToStackExchangeRedis(redis, "DataProtection-Keys");
        }
        else
        {
            services.AddDistributedMemoryCache();
        }

        return services;
    }

    /// <summary>
    ///     Builds the StackExchange.Redis connection settings shared by distributed cache,
    ///     output cache, and IConnectionMultiplexer.
    /// </summary>
    /// <param name="configuration">The application configuration.</param>
    /// <returns>The parsed and configured ConfigurationOptions.</returns>
    public static ConfigurationOptions BuildRedisConfigurationOptions(IConfiguration configuration)
    {
        RedisOptions redisOptions =
            configuration.SectionFor<RedisOptions>().Get<RedisOptions>() ?? new RedisOptions();
        ConfigurationOptions redisConfig = ConfigurationOptions.Parse(
            redisOptions.ConnectionString
        );
        redisConfig.ConnectTimeout = redisOptions.ConnectTimeoutMs;
        redisConfig.SyncTimeout = redisOptions.SyncTimeoutMs;
        redisConfig.AbortOnConnectFail = false;
        return redisConfig;
    }
}
