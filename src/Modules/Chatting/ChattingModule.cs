using Chatting.Features;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Chatting;

public static class ChattingModule
{
    public static IServiceCollection AddChattingModule(
        this IServiceCollection services,
        IConfiguration configuration
    )
    {
        services.AddControllers().AddApplicationPart(typeof(SseController).Assembly);
        return services;
    }

    public static IEndpointRouteBuilder MapChattingEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapControllers();
        return endpoints;
    }
}
