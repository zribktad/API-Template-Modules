using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using SharedKernel.Application.BackgroundJobs;
using SharedKernel.Application.Options.BackgroundJobs;

namespace BackgroundJobs.TickerQ.RecurringJobRegistrations;

public sealed class CleanupRecurringJobRegistration : IRecurringBackgroundJobRegistration
{
    public RecurringBackgroundJobDefinition Build(IServiceProvider serviceProvider)
    {
        BackgroundJobsOptions options = serviceProvider
            .GetRequiredService<IOptions<BackgroundJobsOptions>>()
            .Value;
        return new(
            TickerQJobIds.Cleanup,
            TickerQFunctionNames.Cleanup,
            options.Cleanup.Cron,
            options.Cleanup.Enabled,
            "Runs invitation, soft-delete, and orphaned ProductData cleanup."
        );
    }
}
