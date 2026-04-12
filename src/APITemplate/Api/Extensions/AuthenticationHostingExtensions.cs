using Identity;

namespace APITemplate.Api.Extensions;

/// <summary>
///     Groups the two DI steps that together register JWT Bearer, BFF cookie/OIDC, and authorization
///     policies. The Identity module also registers <c>PostConfigure&lt;JwtBearerOptions&gt;</c> for
///     <see cref="Identity.Auth.Security.IdentityTokenValidatedPipeline" />.
/// </summary>
public static class AuthenticationHostingExtensions
{
    /// <summary>
    ///     Calls <see cref="ApplicationCompositionServiceCollectionExtensions.AddApplicationComposition" />
    ///     then <see cref="IdentityModule.AddIdentityModule" />. Must not be reordered relative to each
    ///     other. The Identity module registers persistence, validators, and output-cache setup in
    ///     addition to authentication.
    /// </summary>
    public static IServiceCollection AddApplicationCompositionAndIdentityModule(
        this IServiceCollection services,
        IConfiguration configuration
    )
    {
        services.AddApplicationComposition(configuration);
        services.AddIdentityModule(configuration);
        return services;
    }
}
