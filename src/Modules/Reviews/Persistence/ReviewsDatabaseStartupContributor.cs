using BuildingBlocks.Infrastructure.EFCore.Persistence;
using BuildingBlocks.Infrastructure.EFCore.Startup;
using Microsoft.Extensions.DependencyInjection;
using Reviews.Persistence;

namespace Reviews.Persistence;

internal sealed class ReviewsDatabaseStartupContributor : IDatabaseStartupContributor
{
    public int Order => 30;

    public async Task ApplyAsync(
        IServiceProvider serviceProvider,
        CancellationToken cancellationToken
    )
    {
        ReviewsDbContext context = serviceProvider.GetRequiredService<ReviewsDbContext>();
        await context.EnsureCreatedAndTablesAsync(cancellationToken);
    }
}
