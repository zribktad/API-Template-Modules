using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace BackgroundJobs.TickerQ.RecurringJobRegistrations;

public sealed class CleanupRecurringJobRegistration : IRecurringBackgroundJobRegistration
{
    public RecurringBackgroundJobDefinition Build(IServiceProvider serviceProvider)
    {
        BackgroundJobsOptions options = serviceProvider
            .GetRequiredService<IOptions<BackgroundJobsOptions>>()
            .Value;
        return new RecurringBackgroundJobDefinition(
            TickerQJobIds.Cleanup,
            TickerQFunctionNames.Cleanup,
            options.Cleanup.Cron,
            options.Cleanup.Enabled,
            "Runs invitation, soft-delete, and orphaned ProductData cleanup."
        );
    }
}
