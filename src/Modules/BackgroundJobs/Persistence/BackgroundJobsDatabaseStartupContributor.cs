using BackgroundJobs.Persistence;
using BuildingBlocks.Infrastructure.EFCore.Persistence;
using BuildingBlocks.Infrastructure.EFCore.Startup;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace BackgroundJobs.Persistence;

internal sealed class BackgroundJobsDatabaseStartupContributor : IDatabaseStartupContributor
{
    public int Order => 50;

    public async Task ApplyAsync(
        IServiceProvider serviceProvider,
        CancellationToken cancellationToken
    )
    {
        BackgroundJobsDbContext context =
            serviceProvider.GetRequiredService<BackgroundJobsDbContext>();
        await context.EnsureCreatedAndTablesAsync(cancellationToken);
    }
}
