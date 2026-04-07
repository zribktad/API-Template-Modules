using Microsoft.Extensions.DependencyInjection;
using Reviews.Persistence;
using SharedKernel.Infrastructure.Persistence;
using SharedKernel.Infrastructure.Startup;

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
