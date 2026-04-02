using FluentValidation;
using Kot.MongoDB.Migrations;
using Kot.MongoDB.Migrations.DI;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Resilience;
using Polly;
using ProductCatalog.Api.Controllers.V1;
using ProductCatalog.Api.GraphQL.DataLoaders;
using ProductCatalog.Api.GraphQL.Mutations;
using ProductCatalog.Api.GraphQL.Queries;
using ProductCatalog.Api.GraphQL.Types;
using ProductCatalog.Application.Features.Product.Repositories;
using ProductCatalog.Application.Features.Product.Validation;
using ProductCatalog.Domain.Interfaces;
using ProductCatalog.Infrastructure.Persistence;
using ProductCatalog.Infrastructure.Repositories;
using ProductCatalog.Infrastructure.SoftDelete;
using ProductCatalog.Infrastructure.StoredProcedures;
using SharedKernel.Application.Batch;
using SharedKernel.Application.Batch.Rules;
using SharedKernel.Application.Resilience;
using SharedKernel.Infrastructure.Configuration;
using SharedKernel.Infrastructure.Health;
using SharedKernel.Infrastructure.Registration;
using ProductApplicationRepository = ProductCatalog.Application.Features.Product.Repositories.IProductRepository;

namespace ProductCatalog.Api;

public static class ProductCatalogModule
{
    public static IServiceCollection AddProductCatalogModule(
        this IServiceCollection services,
        IConfiguration configuration
    )
    {
        string connectionString = configuration.GetConnectionString(
            ConfigurationSections.DefaultConnection
        )!;

        services
            .AddModule<ProductCatalogDbContext>(configuration)
            .ConfigureDbContext(options => options.UseNpgsql(connectionString))
            .AddDefaultInfrastructure()
            .ForwardUnitOfWork<ProductCatalog.Domain.ProductCatalogDbMarker>()
            .AddStoredProcedureSupport()
            .AddRepository<ProductApplicationRepository, ProductRepository>()
            .AddRepository<ICategoryRepository, CategoryRepository>()
            .AddRepository<IProductDataLinkRepository, ProductDataLinkRepository>()
            .AddRepository<IProductDataRepository, ProductDataRepository>()
            .AddCascadeRule<ProductSoftDeleteCascadeRule>();

        services.AddValidatorsFromAssemblyContaining<CreateProductRequestValidator>(
            filter: registration => !registration.ValidatorType.IsGenericTypeDefinition
        );
        services.AddScoped(typeof(IBatchRule<>), typeof(FluentValidationBatchRule<>));

        services.Configure<MongoDbSettings>(
            configuration.GetSection(ConfigurationSections.MongoDB)
        );
        services.AddSingleton<MongoDbContext>();

        MongoDbSettings mongoSettings =
            configuration.GetSection(ConfigurationSections.MongoDB).Get<MongoDbSettings>() ?? new();
        if (
            !string.IsNullOrWhiteSpace(mongoSettings.ConnectionString)
            && !string.IsNullOrWhiteSpace(mongoSettings.DatabaseName)
        )
        {
            services.AddMongoMigrations(
                mongoSettings.ConnectionString,
                new MigrationOptions(mongoSettings.DatabaseName),
                config => config.LoadMigrationsFromAssembly(typeof(ProductCatalogModule).Assembly)
            );
            services.AddHealthChecks().AddCheck<MongoDbHealthCheck>(HealthCheckNames.MongoDb);
        }

        services.AddResiliencePipeline(
            ResiliencePipelineKeys.MongoProductDataDelete,
            builder =>
            {
                builder.AddRetry(
                    new()
                    {
                        MaxRetryAttempts = 3,
                        BackoffType = DelayBackoffType.Exponential,
                        Delay = TimeSpan.FromMilliseconds(200),
                        UseJitter = true,
                    }
                );
            }
        );

        services.AddControllers().AddApplicationPart(typeof(ProductsController).Assembly);

        services
            .AddGraphQLServer()
            .AddQueryType<ProductQueries>()
            .AddTypeExtension<CategoryQueries>()
            .AddTypeExtension<ProductReviewQueries>()
            .AddMutationType<ProductMutations>()
            .AddTypeExtension<ProductReviewMutations>()
            .AddType<ProductType>()
            .AddType<ProductReviewType>()
            .AddDataLoader<ProductReviewsByProductDataLoader>()
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

    public static IEndpointRouteBuilder MapProductCatalogEndpoints(
        this IEndpointRouteBuilder endpoints
    )
    {
        endpoints.MapControllers();
        endpoints.MapGraphQL();
        return endpoints;
    }
}
