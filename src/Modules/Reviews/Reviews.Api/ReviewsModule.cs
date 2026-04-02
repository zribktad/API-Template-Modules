using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Reviews.Api.Controllers.V1;
using Reviews;

namespace Reviews.Api;

public static class ReviewsModule
{
    public static IServiceCollection AddReviewsModule(
        this IServiceCollection services,
        IConfiguration configuration
    )
    {
        services.AddReviewsRuntimeBridge(configuration);
        services.AddControllers().AddApplicationPart(typeof(ProductReviewsController).Assembly);
        return services;
    }

    public static IEndpointRouteBuilder MapReviewsEndpoints(
        this IEndpointRouteBuilder endpoints
    )
    {
        endpoints.MapControllers();
        return endpoints;
    }
}
