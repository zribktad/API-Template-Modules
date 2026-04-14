using BackgroundJobs.Options;
using Microsoft.Extensions.Options;
using SharedKernel.Contracts.Commands.Email;
using Wolverine;

namespace BackgroundJobs.Services;

/// <summary>
///     Infrastructure implementation of <see cref="IEmailRetryJobService" /> that orchestrates
///     scheduled email-retry tasks by dispatching Wolverine commands to the Notifications module.
/// </summary>
public sealed class EmailRetryJobService : IEmailRetryJobService
{
    private readonly IMessageBus _bus;
    private readonly EmailRetryJobOptions _options;

    public EmailRetryJobService(IMessageBus bus, IOptions<BackgroundJobsOptions> options)
    {
        _bus = bus;
        _options = options.Value.EmailRetry;
    }

    public Task RetryFailedEmailsAsync(CancellationToken ct = default) =>
        _bus.InvokeAsync(
            new RetryFailedEmailsCommand(
                _options.MaxRetryAttempts,
                _options.BatchSize,
                _options.ClaimLeaseMinutes
            ),
            ct
        );

    public Task DeadLetterExpiredAsync(CancellationToken ct = default) =>
        _bus.InvokeAsync(
            new DeadLetterExpiredEmailsCommand(
                _options.DeadLetterAfterHours,
                _options.BatchSize,
                _options.ClaimLeaseMinutes
            ),
            ct
        );
}
