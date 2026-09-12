using System.Reflection;
using BuildingBlocks.Application.Modules;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ProductCatalog;

public sealed class ProductCatalogAppModule : IAppModule
{
    public string Name => "ProductCatalog";

    public IEnumerable<Assembly> Assemblies => [typeof(ProductCatalogAppModule).Assembly];

    public void RegisterServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddProductCatalogModule(configuration);
    }
}
