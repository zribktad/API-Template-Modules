using BuildingBlocks.Infrastructure.EFCore.Startup;
using HotChocolate.Execution.Configuration;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.OutputCaching;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Reviews.Configuration;
using Reviews.GraphQL.Mutations;
using Reviews.GraphQL.Queries;
using Reviews.GraphQL.Types;
using Reviews.Persistence;

namespace Reviews;

public static class ReviewsModule
{
    public static IServiceCollection AddReviewsModule(
        this IServiceCollection services,
        IConfiguration configuration
    )
    {
        services.AddReviewsRuntimeBridge(configuration);

        services.AddSingleton<IDatabaseStartupContributor, ReviewsDatabaseStartupContributor>();
        services.AddSingleton<
            IConfigureOptions<OutputCacheOptions>,
            ReviewsOutputCacheOptionsSetup
        >();

        return services;
    }

    public static IRequestExecutorBuilder AddReviewsGraphQL(this IRequestExecutorBuilder builder)
    {
        return builder
            .AddTypeExtension<ProductReviewQueries>()
            .AddTypeExtension<ProductReviewMutations>()
            .AddType<ProductReviewType>();
    }

    public static IEndpointRouteBuilder MapReviewsEndpoints(this IEndpointRouteBuilder endpoints)
    {
        return endpoints;
    }
}
