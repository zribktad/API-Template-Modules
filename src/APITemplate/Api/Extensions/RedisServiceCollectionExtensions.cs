using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.AspNetCore.DataProtection.StackExchangeRedis;
using Microsoft.Extensions.Options;
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

            // Connection established on first use to avoid blocking the startup thread.
            services.AddSingleton<IConnectionMultiplexer>(sp =>
                ConnectionMultiplexer.Connect(sp.GetRequiredService<ConfigurationOptions>())
            );

            services.AddStackExchangeRedisCache(opts =>
            {
                opts.ConfigurationOptions = redisConfiguration;
            });

            services.AddDataProtection().SetApplicationName("APITemplate");

            // Defer key persistence repository creation until Data Protection is first used.
            services.AddSingleton<IConfigureOptions<KeyManagementOptions>>(
                sp => new ConfigureOptions<KeyManagementOptions>(options =>
                {
                    options.XmlRepository = new RedisXmlRepository(
                        () => sp.GetRequiredService<IConnectionMultiplexer>().GetDatabase(),
                        "DataProtection-Keys"
                    );
                })
            );
        }
        else
        {
            services.AddDistributedMemoryCache();

            // Log a warning if Redis is expected but missing, or just info if it's a known fallback.
            // Using a temporary service provider to get a logger during DI registration.
            using ServiceProvider sp = services.BuildServiceProvider();
            ILoggerFactory loggerFactory = sp.GetRequiredService<ILoggerFactory>();
            ILogger logger = loggerFactory.CreateLogger(typeof(RedisServiceCollectionExtensions));
            logger.LogInformation(
                "Redis ConnectionString is not configured. Falling back to in-memory distributed cache and local Data Protection storage."
            );
        }

        return services;
    }
}
