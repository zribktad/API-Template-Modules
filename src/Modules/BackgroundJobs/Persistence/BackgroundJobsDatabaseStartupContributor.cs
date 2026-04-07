using BackgroundJobs.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SharedKernel.Infrastructure.Persistence;
using SharedKernel.Infrastructure.Startup;

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
