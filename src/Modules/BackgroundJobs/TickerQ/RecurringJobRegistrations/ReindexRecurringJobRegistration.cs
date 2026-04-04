using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace BackgroundJobs.TickerQ.RecurringJobRegistrations;

public sealed class ReindexRecurringJobRegistration : IRecurringBackgroundJobRegistration
{
    public RecurringBackgroundJobDefinition Build(IServiceProvider serviceProvider)
    {
        BackgroundJobsOptions options = serviceProvider
            .GetRequiredService<IOptions<BackgroundJobsOptions>>()
            .Value;
        return new RecurringBackgroundJobDefinition(
            TickerQJobIds.Reindex,
            TickerQFunctionNames.Reindex,
            options.Reindex.Cron,
            options.Reindex.Enabled,
            "Rebuilds the PostgreSQL full-text search indexes."
        );
    }
}
