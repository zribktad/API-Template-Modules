using BuildingBlocks.Application.Configuration;
using BuildingBlocks.Infrastructure.Redis.Configuration;
using BuildingBlocks.Web.Configuration;
using BuildingBlocks.Web.Health;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ProductCatalog.Persistence;

namespace ProductCatalog.Infrastructure.Health;

public sealed class ProductCatalogHealthChecks : IHealthCheckModule
{
    private readonly IConfiguration _configuration;

    public ProductCatalogHealthChecks(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public void RegisterHealthChecks(IHealthChecksBuilder builder)
    {
        MongoDbSettings? mongoSettings = _configuration
            .GetSection(ConfigurationSections.MongoDB)
            .Get<MongoDbSettings>();

        if (
            mongoSettings is not null
            && !string.IsNullOrWhiteSpace(mongoSettings.ConnectionString)
            && !string.IsNullOrWhiteSpace(mongoSettings.DatabaseName)
        )
        {
            builder.AddCheck<MongoDbHealthCheck>(
                HealthCheckNames.MongoDb,
                tags: [HealthCheckTags.Ready, HealthCheckTags.Database]
            );
        }
    }
}
