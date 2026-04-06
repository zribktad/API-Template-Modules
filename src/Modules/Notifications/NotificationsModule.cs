using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Notifications;

public static class NotificationsModule
{
    public static IServiceCollection AddNotificationsModule(
        this IServiceCollection services,
        IConfiguration configuration
    )
    {
        services.AddNotificationsRuntimeBridge(configuration);
        // Event-driven so no controllers.
        // services.AddControllers().AddApplicationPart(typeof(SomeController).Assembly);
        return services;
    }

    /// <summary>Notifications are event-driven (Wolverine); no HTTP routes to map.</summary>
    public static IEndpointRouteBuilder MapNotificationsEndpoints(
        this IEndpointRouteBuilder endpoints
    ) => endpoints;
}
