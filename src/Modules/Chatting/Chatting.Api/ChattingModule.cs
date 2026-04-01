using Chatting.Api.Controllers.V1;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Chatting.Api;

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
