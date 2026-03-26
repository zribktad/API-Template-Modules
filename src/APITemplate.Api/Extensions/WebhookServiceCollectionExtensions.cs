using APITemplate.Api.Extensions.Resilience;
using APITemplate.Application.Common.BackgroundJobs;
using APITemplate.Application.Common.Contracts;
using APITemplate.Application.Common.Options;
using APITemplate.Application.Common.Resilience;
using APITemplate.Application.Features.Examples.DTOs;
using APITemplate.Infrastructure.Webhooks;
using Microsoft.Extensions.Http.Resilience;
using Polly;

namespace APITemplate.Api.Extensions;

/// <summary>
/// Presentation-layer extension class that registers both incoming (HMAC-validated, channel-queued)
/// and outgoing (signed, HTTP-delivered) webhook infrastructure services.
/// </summary>
public static class WebhookServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="WebhookOptions"/>, the HMAC payload validator, the inbound channel
    /// queue with its background processor, and the logging webhook event handler.
    /// </summary>
    public static IServiceCollection AddIncomingWebhookServices(
        this IServiceCollection services,
        IConfiguration configuration
    )
    {
        services.AddValidatedOptions<WebhookOptions>(configuration);

        services.AddSingleton<IWebhookPayloadValidator, HmacWebhookPayloadValidator>();
        services.AddQueueWithConsumer<
            ChannelWebhookQueue,
            IWebhookProcessingQueue,
            IWebhookQueueReader,
            WebhookProcessingBackgroundService
        >();
        services.AddScoped<IWebhookEventHandler, LoggingWebhookEventHandler>();
        return services;
    }

    /// <summary>
    /// Registers the HMAC payload signer, the outbound channel queue with its background delivery
    /// service, and an HTTP client with a Polly exponential-backoff retry pipeline for failed deliveries.
    /// </summary>
    public static IServiceCollection AddOutgoingWebhookServices(this IServiceCollection services)
    {
        services.AddSingleton<IWebhookPayloadSigner, HmacWebhookPayloadSigner>();

        services.AddQueueWithConsumer<
            ChannelOutgoingWebhookQueue,
            IOutgoingWebhookQueue,
            IOutgoingWebhookQueueReader,
            OutgoingWebhookBackgroundService
        >();

        services
            .AddHttpClient(WebhookConstants.OutgoingHttpClientName)
            .AddResilienceHandler(
                ResiliencePipelineKeys.OutgoingWebhook,
                builder =>
                {
                    builder.AddRetry(
                        new HttpRetryStrategyOptions
                        {
                            MaxRetryAttempts = ResilienceDefaults.MaxRetryAttempts,
                            BackoffType = DelayBackoffType.Exponential,
                            Delay = ResilienceDefaults.LongDelay,
                            UseJitter = true,
                        }
                    );
                }
            );

        return services;
    }
}
