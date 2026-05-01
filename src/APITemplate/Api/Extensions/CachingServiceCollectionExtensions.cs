using APITemplate.Api.Cache;
using Microsoft.AspNetCore.OutputCaching.StackExchangeRedis;
using SharedKernel.Application.Options.Http;
using SharedKernel.Infrastructure.Configuration;
using SharedKernel.Infrastructure.OutputCache;
using StackExchange.Redis;

namespace APITemplate.Api.Extensions;

/// <summary>
///     Provides extension methods for configuring application caching.
/// </summary>
public static class CachingServiceCollectionExtensions
{
    /// <summary>
    ///     Registers output caching policies and infrastructure, integrating with Redis if configured.
    /// </summary>
    /// <param name="services">The IServiceCollection to add services to.</param>
    /// <param name="configuration">The application configuration.</param>
    /// <returns>The updated IServiceCollection.</returns>
    public static IServiceCollection AddCaching(
        this IServiceCollection services,
        IConfiguration configuration
    )
    {
        services.AddValidatedOptions<CachingOptions>(configuration);
        services.AddSingleton<TenantAwareOutputCachePolicy>();
        services.AddScoped<IOutputCacheInvalidationService, OutputCacheInvalidationService>();

        services.AddOutputCache(options => options.AddBasePolicy(builder => builder.NoCache()));

        if (configuration.IsRedisConfigured())
        {
            services
                .AddOptions<RedisOutputCacheOptions>()
                .Configure<ConfigurationOptions>(
                    (options, redisConfig) =>
                    {
                        options.ConfigurationOptions = redisConfig;
                        options.InstanceName = RedisInstanceNames.OutputCache;
                    }
                );

            services.AddStackExchangeRedisOutputCache(_ => { });
        }

        return services;
    }
}
