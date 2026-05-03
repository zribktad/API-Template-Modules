using BackgroundJobs.TickerQ;
using BuildingBlocks.Infrastructure.EFCore.Persistence;
using BuildingBlocks.Infrastructure.EFCore.Startup;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace BackgroundJobs.TickerQ;

/// <summary>
///     TickerQ scheduler context is only registered when TickerQ is enabled.
/// </summary>
internal sealed class TickerQSchedulerDatabaseStartupContributor : IDatabaseStartupContributor
{
    public int Order => 70;

    public async Task ApplyAsync(
        IServiceProvider serviceProvider,
        CancellationToken cancellationToken
    )
    {
        TickerQSchedulerDbContext? context =
            serviceProvider.GetService<TickerQSchedulerDbContext>();
        if (context is null)
            return;

        await context.EnsureCreatedAndTablesAsync(cancellationToken);
    }
}
