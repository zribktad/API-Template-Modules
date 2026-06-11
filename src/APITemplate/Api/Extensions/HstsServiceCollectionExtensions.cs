using BuildingBlocks.Application.Options.Http;

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
        // Only the validated ApiHstsOptions are needed; the HSTS header is emitted by
        // UseSecurityHeadersPolicy (SecurityHeadersExtensions), not the framework's UseHsts()
        // middleware (which is never wired up), so AddHsts()/HstsOptions setup would be dead code.
        services.AddValidatedOptions<ApiHstsOptions>(configuration.GetSection("Hsts"));

        return services;
    }
}
