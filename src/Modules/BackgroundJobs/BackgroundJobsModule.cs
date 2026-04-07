using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace BackgroundJobs;

public static class BackgroundJobsModule
{
    public static IServiceCollection AddBackgroundJobsModule(
        this IServiceCollection services,
        IConfiguration configuration
    )
    {
        services.AddBackgroundJobsRuntimeBridge(configuration);
        services.AddControllers().AddApplicationPart(typeof(JobsController).Assembly);

        return services;
    }

    public static IEndpointRouteBuilder MapBackgroundJobsEndpoints(
        this IEndpointRouteBuilder endpoints
    )
    {
        return endpoints;
    }
}
