using APITemplate.Api.Security;
using BuildingBlocks.Application.Context;
using BuildingBlocks.Application.Http;
using Identity.Auth.Security;

namespace APITemplate.Api.Extensions;

/// <summary>
///     Provides extension methods for configuring request context services.
/// </summary>
public static class RequestContextServiceCollectionExtensions
{
    /// <summary>
    ///     Registers IHttpContextAccessor and the single per-request adapter that exposes the
    ///     authenticated user's identity, actor, and tenant to the application layer via three interfaces.
    /// </summary>
    /// <param name="services">The IServiceCollection to add services to.</param>
    /// <returns>The updated IServiceCollection.</returns>
    public static IServiceCollection AddRequestContext(this IServiceCollection services)
    {
        services.AddHttpContextAccessor();
        services.AddScoped<HttpRequestIdentityProvider>();
        services.AddScoped<ICurrentRequestUser>(sp =>
            sp.GetRequiredService<HttpRequestIdentityProvider>()
        );
        services.AddScoped<IActorProvider>(sp =>
            sp.GetRequiredService<HttpRequestIdentityProvider>()
        );
        services.AddScoped<ITenantProvider>(sp =>
            sp.GetRequiredService<HttpRequestIdentityProvider>()
        );

        return services;
    }
}
