using BuildingBlocks.Application.Configuration;
using BuildingBlocks.Application.Constants;
using BuildingBlocks.Application.Options;
using BuildingBlocks.Application.Resilience;
using BuildingBlocks.Infrastructure.EFCore.Registration;
using BuildingBlocks.Web.Configuration;
using BuildingBlocks.Web.Resilience;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;
using Polly;
using Webhooks.Contracts;
using Webhooks.Persistence;
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
        string connectionString = configuration.GetConnectionString(
            ConfigurationSections.DefaultConnection
        )!;

        services
            .AddModule<WebhooksDbContext>(configuration)
            .ConfigureDbContext(options => options.UseNpgsql(connectionString))
            .AddDefaultInfrastructure()
            .ForwardUnitOfWork<WebhooksDbMarker>();

        AddIncomingWebhookServices(services, configuration);
        AddOutgoingWebhookServices(services);
        return services;
    }

    private static void AddIncomingWebhookServices(
        IServiceCollection services,
        IConfiguration configuration
    )
    {
        services.AddModuleOptions<WebhookOptions>(configuration);
        services.AddSingleton<IWebhookPayloadValidator, HmacWebhookPayloadValidator>();
        // Inbound payloads are processed by the durable Wolverine IncomingWebhookHandler
        // (auto-discovered) instead of an in-memory channel + hosted consumer.
        services.AddScoped<IWebhookEventHandler, LoggingWebhookEventHandler>();
    }

    private static void AddOutgoingWebhookServices(IServiceCollection services)
    {
        services.AddSingleton<INetworkSecurityPolicy, DefaultNetworkSecurityPolicy>();
        services.AddSingleton<IWebhookPayloadSigner, HmacWebhookPayloadSigner>();
        // Outgoing deliveries run inside the durable Wolverine SendWebhookCallbackHandler
        // (auto-discovered) instead of an in-memory channel + hosted consumer.

        services
            .AddHttpClient(
                WebhookConstants.OutgoingHttpClientName,
                client =>
                {
                    client.Timeout = TimeSpan.FromSeconds(30);
                }
            )
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
