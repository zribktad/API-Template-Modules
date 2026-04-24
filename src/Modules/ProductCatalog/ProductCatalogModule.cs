using Kot.MongoDB.Migrations;
using Kot.MongoDB.Migrations.DI;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.OutputCaching;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Polly;
using Polly.Retry;
using ProductCatalog.Configuration;
using ProductCatalog.Domain.Services;
using ProductCatalog.GraphQL.DataLoaders;
using ProductCatalog.GraphQL.Mutations;
using ProductCatalog.GraphQL.Queries;
using ProductCatalog.GraphQL.Types;
using ProductCatalog.Infrastructure.Health;
using ProductCatalog.Persistence;
using ProductCatalog.Persistence.Interceptors;
using ProductCatalog.Repositories;
using ProductCatalog.Services;
using SharedKernel.Application.Batch;
using SharedKernel.Application.Batch.Rules;
using SharedKernel.Application.Resilience;
using SharedKernel.Infrastructure.Configuration;
using SharedKernel.Infrastructure.Health;
using SharedKernel.Infrastructure.Persistence;
using SharedKernel.Infrastructure.Registration;
using SharedKernel.Infrastructure.Startup;
using ProductApplicationRepository = ProductCatalog.Interfaces.IProductRepository;

namespace ProductCatalog;

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
            .AddScoped<ProductLinkSoftDeleteCascadeInterceptor>()
            .ConfigureDbContext((sp, options) =>
                options
                    .UseNpgsql(connectionString)
                    .AddInterceptors(
                        sp.GetRequiredService<ProductLinkSoftDeleteCascadeInterceptor>()
                    )
            )
            .AddDefaultInfrastructure()
            .ForwardUnitOfWork<ProductCatalogDbMarker>()
            .AddStoredProcedureSupport()
            .AddRepository<ProductApplicationRepository, ProductRepository>()
            .AddRepository<ICategoryRepository, CategoryRepository>()
            .AddRepository<IProductDataLinkRepository, ProductDataLinkRepository>()
            .AddRepository<IProductDataRepository, ProductDataRepository>();

        services.AddScoped<IProductReferenceValidator, ProductReferenceValidator>();
        services.AddScoped(typeof(IProductBatchValidator<>), typeof(ProductBatchValidator<>));
        services.AddScoped<IProductBatchFactory, ProductBatchFactory>();

        MongoSerializationConfiguration.Configure();

        services.Configure<MongoDbSettings>(
            configuration.GetSection(ConfigurationSections.MongoDB)
        );
        services.AddSingleton<MongoDbContext>();

        MongoDbSettings mongoSettings =
            configuration.GetSection(ConfigurationSections.MongoDB).Get<MongoDbSettings>()
            ?? new MongoDbSettings();
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
        }

        services.AddResiliencePipeline(
            ResiliencePipelineKeys.MongoProductDataDelete,
            builder =>
            {
                builder.AddRetry(
                    new RetryStrategyOptions
                    {
                        MaxRetryAttempts = 3,
                        BackoffType = DelayBackoffType.Exponential,
                        Delay = TimeSpan.FromMilliseconds(200),
                        UseJitter = true,
                    }
                );
            }
        );
        services.AddSingleton<
            IMongoProductDataDeletePipelineProvider,
            MongoProductDataDeletePipelineProvider
        >();

        services.AddControllers().AddApplicationPart(typeof(ProductCatalogModule).Assembly);

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

        services.AddSingleton<
            IDatabaseStartupContributor,
            ProductCatalogDatabaseStartupContributor
        >();
        services.AddSingleton<
            IConfigureOptions<OutputCacheOptions>,
            ProductCatalogOutputCacheOptionsSetup
        >();

        return services;
    }

    public static IEndpointRouteBuilder MapProductCatalogEndpoints(
        this IEndpointRouteBuilder endpoints
    )
    {
        endpoints.MapGraphQL();
        return endpoints;
    }
}
