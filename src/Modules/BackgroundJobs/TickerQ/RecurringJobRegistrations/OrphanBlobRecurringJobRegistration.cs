using BackgroundJobs.Options;
using Microsoft.Extensions.Options;

namespace BackgroundJobs.TickerQ.RecurringJobRegistrations;

public sealed class OrphanBlobRecurringJobRegistration : IRecurringBackgroundJobRegistration
{
    private readonly IOptions<BackgroundJobsOptions> _options;

    public OrphanBlobRecurringJobRegistration(IOptions<BackgroundJobsOptions> options)
    {
        _options = options;
    }

    public RecurringBackgroundJobDefinition Build()
    {
        OrphanBlobJobOptions options = _options.Value.OrphanBlob;

        return new RecurringBackgroundJobDefinition(
            new Guid("7e5b7aa1-8b8d-4c74-8b5d-5b1f9b8c3a42"),
            "orphan-blob-recurring-job",
            options.Cron,
            options.Enabled,
            "Sweeps orphan staging payloads and zero-refcount blobs from the FileStorage module."
        );
    }
}
