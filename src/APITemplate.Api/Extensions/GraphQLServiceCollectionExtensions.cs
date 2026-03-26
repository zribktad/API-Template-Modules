using APITemplate.Api.GraphQL.Instrumentation;

namespace APITemplate.Api.Extensions;

/// <summary>
/// Presentation-layer extension class that configures the Hot Chocolate GraphQL server,
/// registering query/mutation types, object type mappings, data loaders, authorization,
/// instrumentation, and paging/depth-limit rules.
/// </summary>
public static class GraphQLServiceCollectionExtensions
{
    /// <summary>
    /// Adds the GraphQL server with product and review query/mutation types, object type
    /// configurations, the batch data loader, metrics listener, and a max execution depth of 5.
    /// </summary>
    public static IServiceCollection AddGraphQLConfiguration(this IServiceCollection services)
    {
        services.AddSingleton<GraphQlExecutionMetricsListener>();

        services
            .AddGraphQLServer()
            .AddQueryType<GraphQL.Queries.ProductQueries>()
            .AddTypeExtension<GraphQL.Queries.CategoryQueries>()
            .AddTypeExtension<GraphQL.Queries.ProductReviewQueries>()
            .AddMutationType<GraphQL.Mutations.ProductMutations>()
            .AddTypeExtension<GraphQL.Mutations.ProductReviewMutations>()
            .AddType<GraphQL.Types.ProductType>()
            .AddType<GraphQL.Types.ProductReviewType>()
            .AddDataLoader<GraphQL.DataLoaders.ProductReviewsByProductDataLoader>()
            .AddAuthorization()
            .AddInstrumentation()
            .AddDiagnosticEventListener(sp =>
                sp.GetRequiredService<GraphQlExecutionMetricsListener>()
            )
            .ModifyPagingOptions(o =>
            {
                o.MaxPageSize = PaginationFilter.MaxPageSize;
                o.DefaultPageSize = PaginationFilter.DefaultPageSize;
                o.IncludeTotalCount = true;
            })
            .AddMaxExecutionDepthRule(5);

        return services;
    }
}
