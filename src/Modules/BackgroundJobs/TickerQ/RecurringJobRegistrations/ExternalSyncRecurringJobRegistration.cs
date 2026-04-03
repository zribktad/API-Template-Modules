using SharedKernel.Application.Options.BackgroundJobs;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using SharedKernel.Application.BackgroundJobs;

namespace BackgroundJobs.TickerQ.RecurringJobRegistrations;

public sealed class ExternalSyncRecurringJobRegistration : IRecurringBackgroundJobRegistration
{
    public RecurringBackgroundJobDefinition Build(IServiceProvider serviceProvider)
    {
        BackgroundJobsOptions options = serviceProvider.GetRequiredService<IOptions<BackgroundJobsOptions>>().Value;
        return new(
            TickerQJobIds.ExternalSync,
            TickerQFunctionNames.ExternalSync,
            options.ExternalSync.Cron,
            options.ExternalSync.Enabled,
            "Runs periodic synchronization for configured external integrations."
        );
    }
}


