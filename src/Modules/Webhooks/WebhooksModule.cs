using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Webhooks.Features.ReceiveWebhook;
using Webhooks.Shared;

namespace Webhooks;

public static class WebhooksModule
{
    public static IServiceCollection AddWebhooksModule(
        this IServiceCollection services,
        IConfiguration configuration
    )
    {
        services.AddWebhooksRuntimeBridge(configuration);
        services.AddScoped<WebhookSignatureResourceFilter>();
        services.AddControllers(options =>
        {
            options.Filters.AddService<WebhookSignatureResourceFilter>();
        }).AddApplicationPart(typeof(WebhooksController).Assembly);

        return services;
    }

    public static IEndpointRouteBuilder MapWebhooksEndpoints(
        this IEndpointRouteBuilder endpoints
    )
    {
        endpoints.MapControllers();
        return endpoints;
    }
}

