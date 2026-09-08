using BuildingBlocks.Application.Constants;
using BuildingBlocks.Application.DTOs;
using HotChocolate;
using HotChocolate.Execution.Configuration;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ProductCatalog;
using Reviews;

namespace APITemplate.Api.Extensions;

public static class GraphQLServiceCollectionExtensions
{
    public static IServiceCollection AddGraphQLRegistration(
        this IServiceCollection services,
        IWebHostEnvironment environment,
        IConfiguration configuration
    )
    {
        bool enableIntrospection =
            configuration.GetValue<bool?>("GraphQL:EnableIntrospection")
            ?? environment.IsDevelopment();

        IRequestExecutorBuilder builder = services
            .AddGraphQLServer()
            .AddQueryType(d => d.Name(HotChocolate.Types.OperationTypeNames.Query))
            .AddMutationType(d => d.Name(HotChocolate.Types.OperationTypeNames.Mutation))
            .AddProductCatalogGraphQL()
            .AddReviewsGraphQL()
            .AddAuthorization()
            .ModifyPagingOptions(options =>
            {
                options.MaxPageSize = PaginationFilter.MaxPageSize;
                options.DefaultPageSize = PaginationFilter.DefaultPageSize;
                options.IncludeTotalCount = true;
            })
            .AddMaxExecutionDepthRule(GraphQLConstants.MaxExecutionDepth) // Prevent deeply nested query DoS
            .AddCostAnalyzer() // Enable cost analysis
            .ModifyCostOptions(options =>
            {
                options.MaxFieldCost = GraphQLConstants.MaxFieldCost; // Prevent high-complexity query DoS
                options.EnforceCostLimits = true;
            });

        if (!enableIntrospection)
        {
            builder.DisableIntrospection(); // Disable introspection when not explicitly enabled
        }

        return services;
    }
}
