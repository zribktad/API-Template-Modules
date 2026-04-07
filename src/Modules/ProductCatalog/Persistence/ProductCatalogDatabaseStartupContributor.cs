using Microsoft.Extensions.DependencyInjection;
using ProductCatalog.Persistence;
using SharedKernel.Infrastructure.Persistence;
using SharedKernel.Infrastructure.Startup;

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
