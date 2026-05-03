using BuildingBlocks.Application.Configuration;
using BuildingBlocks.Application.Options.Http;
using Microsoft.AspNetCore.HttpsPolicy;
using Microsoft.Extensions.Options;

namespace APITemplate.Api.Extensions;

/// <summary>
///     Provides extension methods for configuring HSTS.
/// </summary>
public static class HstsServiceCollectionExtensions
{
    /// <summary>
    ///     Registers and configures HSTS options with validation.
    /// </summary>
    /// <param name="services">The IServiceCollection to add services to.</param>
    /// <param name="configuration">The application configuration.</param>
    /// <returns>The updated IServiceCollection.</returns>
    public static IServiceCollection AddHstsRegistration(
        this IServiceCollection services,
        IConfiguration configuration
    )
    {
        services.AddValidatedOptions<ApiHstsOptions>(configuration.GetSection("Hsts"));

        services.AddSingleton<IConfigureOptions<HstsOptions>, HstsOptionsSetup>();
        services.AddHsts(_ => { });

        return services;
    }

    private sealed class HstsOptionsSetup(IOptions<ApiHstsOptions> hstsOptions)
        : IConfigureOptions<HstsOptions>
    {
        public void Configure(HstsOptions options)
        {
            ApiHstsOptions o = hstsOptions.Value;
            options.Preload = o.Preload;
            options.IncludeSubDomains = o.IncludeSubDomains;
            options.MaxAge = TimeSpan.FromDays(o.MaxAgeDays);
        }
    }
}
