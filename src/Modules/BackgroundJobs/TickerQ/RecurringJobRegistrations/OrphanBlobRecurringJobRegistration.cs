using BackgroundJobs.Options;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace BackgroundJobs.TickerQ.RecurringJobRegistrations;

public sealed class OrphanBlobRecurringJobRegistration : IRecurringBackgroundJobRegistration
{
    public RecurringBackgroundJobDefinition Build(IServiceProvider serviceProvider)
    {
        OrphanBlobJobOptions options = serviceProvider
            .GetRequiredService<IOptions<BackgroundJobsOptions>>()
            .Value.OrphanBlob;

        return new RecurringBackgroundJobDefinition(
            new Guid("7e5b7aa1-8b8d-4c74-8b5d-5b1f9b8c3a42"),
            "orphan-blob-recurring-job",
            options.Cron,
            options.Enabled,
            "Sweeps orphan staging payloads and zero-refcount blobs from the FileStorage module."
        );
    }
}
