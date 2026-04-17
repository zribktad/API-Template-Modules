using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;
using Polly;
using SharedKernel.Application.Resilience;
using SharedKernel.Infrastructure.Configuration;
using SharedKernel.Infrastructure.Registration;
using SharedKernel.Infrastructure.Resilience;
using Webhooks.Contracts;
using Webhooks.Security;
using Webhooks.Services;

namespace Webhooks;

public static class WebhooksRuntimeBridge
{
    public static IServiceCollection AddWebhooksRuntimeBridge(
        this IServiceCollection services,
        IConfiguration configuration
    )
    {
        AddIncomingWebhookServices(services, configuration);
        AddOutgoingWebhookServices(services);
        return services;
    }

    private static void AddIncomingWebhookServices(
        IServiceCollection services,
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
    }

    private static void AddOutgoingWebhookServices(IServiceCollection services)
    {
        services.AddSingleton<INetworkSecurityPolicy, DefaultNetworkSecurityPolicy>();
        services.AddSingleton<IWebhookPayloadSigner, HmacWebhookPayloadSigner>();
        services.AddQueueWithConsumer<
            ChannelOutgoingWebhookQueue,
            IOutgoingWebhookQueue,
            IOutgoingWebhookQueueReader,
            OutgoingWebhookBackgroundService
        >();

        services
            .AddHttpClient(WebhookConstants.OutgoingHttpClientName)
            .ConfigurePrimaryHttpMessageHandler(sp =>
                SsrfProtectedSocketsHttpHandlerFactory.Create(
                    sp.GetRequiredService<INetworkSecurityPolicy>()
                )
            )
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
    }
}
