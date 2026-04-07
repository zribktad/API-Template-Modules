using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.OutputCaching;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Reviews.Configuration;
using Reviews.Persistence;
using SharedKernel.Infrastructure.Startup;

namespace Reviews;

public static class ReviewsModule
{
    public static IServiceCollection AddReviewsModule(
        this IServiceCollection services,
        IConfiguration configuration
    )
    {
        services.AddReviewsRuntimeBridge(configuration);
        services.AddControllers().AddApplicationPart(typeof(ProductReviewsController).Assembly);

        services.AddSingleton<IDatabaseStartupContributor, ReviewsDatabaseStartupContributor>();
        services.AddSingleton<
            IConfigureOptions<OutputCacheOptions>,
            ReviewsOutputCacheOptionsSetup
        >();

        return services;
    }

    public static IEndpointRouteBuilder MapReviewsEndpoints(this IEndpointRouteBuilder endpoints)
    {
        return endpoints;
    }
}
