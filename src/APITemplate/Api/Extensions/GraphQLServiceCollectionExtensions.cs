using HotChocolate.Execution.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ProductCatalog;
using Reviews;
using SharedKernel.Application.DTOs;

namespace APITemplate.Api.Extensions;

public static class GraphQLServiceCollectionExtensions
{
    public static IServiceCollection AddGraphQLRegistration(this IServiceCollection services)
    {
        services
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
            .AddMaxExecutionDepthRule(5);

        return services;
    }
}
