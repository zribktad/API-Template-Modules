using APITemplate.Api.Extensions;
using BuildingBlocks.Application.Configuration;
using BuildingBlocks.Application.Options;
using BuildingBlocks.Application.Options.Infrastructure;
using Identity.Persistence;
using Microsoft.AspNetCore.DataProtection;
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
        services.AddModuleOptions<RedisOptions>(configuration);

        // Data Protection keys are stored persistently in the database to separate them from volatile cache.
        // This ensures sessions remain valid even if the cache is cleared or restarts.
        services
            .AddDataProtection()
            .SetApplicationName("APITemplate")
            .PersistKeysToDbContext<IdentityDbContext>();

        if (configuration.IsRedisConfigured())
        {
            ConfigurationOptions redisConfiguration =
                configuration.BuildRedisConfigurationOptions();
            services.AddSingleton(redisConfiguration);

            // Connection established on first use to avoid blocking the startup thread.
            services.AddSingleton<IConnectionMultiplexer>(sp =>
                ConnectionMultiplexer.Connect(sp.GetRequiredService<ConfigurationOptions>())
            );

            services.AddStackExchangeRedisCache(opts =>
            {
                opts.ConfigurationOptions = redisConfiguration;
            });
        }
        else
        {
            // Fallback to in-memory cache if Redis is not configured.
            services.AddDistributedMemoryCache();
        }

        return services;
    }
}
