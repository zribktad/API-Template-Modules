using BackgroundJobs.TickerQ;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SharedKernel.Infrastructure.Persistence;
using SharedKernel.Infrastructure.Startup;

namespace BackgroundJobs.TickerQ;

/// <summary>
///     TickerQ scheduler context is only registered when TickerQ and Dragonfly are enabled.
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
