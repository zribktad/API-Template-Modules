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
    /// <returns>The updated IServiceCollection.</returns>
    public static IServiceCollection AddRedisInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration
    )
    {
        services.AddValidatedOptions<RedisOptions>(configuration);

        if (configuration.IsRedisConfigured())
        {
            ConfigurationOptions redisConfiguration =
                configuration.BuildRedisConfigurationOptions();
            services.AddSingleton(redisConfiguration);

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
}
