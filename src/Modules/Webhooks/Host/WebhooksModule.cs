using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Webhooks.Host.Features.ReceiveWebhook;
using Webhooks.Host.Shared;

namespace Webhooks.Host;

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
