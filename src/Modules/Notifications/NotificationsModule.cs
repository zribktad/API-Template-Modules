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

    public static IEndpointRouteBuilder MapNotificationsEndpoints(
        this IEndpointRouteBuilder endpoints
    )
    {
        // endpoints.MapControllers();
        return endpoints;
    }
}
