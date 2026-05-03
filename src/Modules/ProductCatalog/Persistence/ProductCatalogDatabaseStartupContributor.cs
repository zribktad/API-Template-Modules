using BuildingBlocks.Infrastructure.EFCore.Persistence;
using BuildingBlocks.Infrastructure.EFCore.Startup;
using Microsoft.Extensions.DependencyInjection;
using ProductCatalog.Persistence;

namespace ProductCatalog.Persistence;

internal sealed class ProductCatalogDatabaseStartupContributor : IDatabaseStartupContributor
{
    public int Order => 20;

    public async Task ApplyAsync(
        IServiceProvider serviceProvider,
        CancellationToken cancellationToken
    )
    {
        ProductCatalogDbContext context =
            serviceProvider.GetRequiredService<ProductCatalogDbContext>();
        await context.EnsureCreatedAndTablesAsync(cancellationToken);
    }
}
