using APITemplate.Api.Extensions.Resilience;
using APITemplate.Application.Common.Options;
using APITemplate.Application.Common.Resilience;
using APITemplate.Application.Common.Security;
using APITemplate.Infrastructure.Security;
using Keycloak.AuthServices.Sdk;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.Extensions.Options;
using Polly;

namespace APITemplate.Api.Extensions;

/// <summary>
/// Presentation-layer extension class that registers the Keycloak Admin HTTP client with
/// machine-to-machine token injection, exponential-backoff resilience, and the
/// <see cref="IKeycloakAdminService"/> scoped service.
/// </summary>
public static class KeycloakAdminServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="KeycloakAdminClientOptions"/> populated from <see cref="KeycloakOptions"/>,
    /// adds the SDK's named HTTP client with a token-handler delegate and a Polly retry pipeline,
    /// and registers <see cref="IKeycloakAdminService"/> as a scoped service.
    /// </summary>
    public static IServiceCollection AddKeycloakAdminService(this IServiceCollection services)
    {
        // Populate KeycloakAdminClientOptions from IOptions<KeycloakOptions> at runtime,
        // so validation runs through the IOptions pipeline rather than raw IConfiguration.
        services
            .AddOptions<KeycloakAdminClientOptions>()
            .Configure<IOptions<KeycloakOptions>>(
                (adminOpts, keycloakOpts) =>
                {
                    adminOpts.AuthServerUrl = keycloakOpts.Value.AuthServerUrl;
                    adminOpts.Realm = keycloakOpts.Value.Realm;
                }
            );

        services.AddSingleton<KeycloakAdminTokenProvider>();
        services.AddTransient<KeycloakAdminTokenHandler>();

        // Pass a no-op action so the SDK registers its IKeycloakClient registrations and
        // the named HttpClient; the actual option values come from the Configure call above.
        services
            .AddKeycloakAdminHttpClient(_ => { })
            .AddHttpMessageHandler<KeycloakAdminTokenHandler>()
            .AddResilienceHandler(
                ResiliencePipelineKeys.KeycloakAdmin,
                builder =>
                {
                    builder.AddRetry(
                        new HttpRetryStrategyOptions
                        {
                            MaxRetryAttempts = ResilienceDefaults.MaxRetryAttempts,
                            BackoffType = DelayBackoffType.Exponential,
                            Delay = ResilienceDefaults.ShortDelay,
                            UseJitter = true,
                        }
                    );
                }
            );

        services.AddScoped<IKeycloakAdminService, KeycloakAdminService>();

        return services;
    }
}
