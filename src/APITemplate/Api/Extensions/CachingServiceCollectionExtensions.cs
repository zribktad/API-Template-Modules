using APITemplate.Api.Cache;
using SharedKernel.Application.Http;
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
    /// <param name="redisConfiguration">The parsed Redis configuration options, if available.</param>
    /// <returns>The updated IServiceCollection.</returns>
    public static IServiceCollection AddCaching(
        this IServiceCollection services,
        IConfiguration configuration,
        ConfigurationOptions? redisConfiguration
    )
    {
        services.AddValidatedOptions<CachingOptions>(configuration);
        services.AddSingleton<TenantAwareOutputCachePolicy>();
        services.AddScoped<IOutputCacheInvalidationService, OutputCacheInvalidationService>();

        services.AddOutputCache(options => options.AddBasePolicy(builder => builder.NoCache()));

        if (redisConfiguration is not null)
        {
            services.AddStackExchangeRedisOutputCache(options =>
            {
                options.ConfigurationOptions = redisConfiguration;
                options.InstanceName = RedisInstanceNames.OutputCache;
            });
        }

        return services;
    }
}
