using System.Net.Http;
using BuildingBlocks.Web.Resilience;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;
using Polly;

namespace Identity.Auth.Http;

/// <summary>
///     Resilience registration for Keycloak HTTP clients (token and admin).
/// </summary>
public static class KeycloakHttpClientBuilderExtensions
{
    public static IHttpResiliencePipelineBuilder AddKeycloakHttpRetry(
        this IHttpClientBuilder httpClientBuilder,
        string pipelineKey
    )
    {
        return httpClientBuilder.AddResilienceHandler(pipelineKey, ConfigureKeycloakHttpRetry);
    }

    private static void ConfigureKeycloakHttpRetry(
        ResiliencePipelineBuilder<HttpResponseMessage> builder
    )
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
}
