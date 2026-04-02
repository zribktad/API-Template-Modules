using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Notifications.Application.Common.BackgroundJobs;
using Notifications.Infrastructure.Logging;
using SharedKernel.Application.BackgroundJobs;
using SharedKernel.Application.Options.BackgroundJobs;
using TickerQ.Utilities.Base;

namespace Notifications.Infrastructure.BackgroundJobs.TickerQ.Jobs;

/// <summary>
/// TickerQ recurring job that retries previously failed emails and dead-letters those that have
/// exceeded the configured retry window, delegating to <see cref="IEmailRetryService"/>.
/// Execution is gated by <see cref="IDistributedJobCoordinator"/> to prevent multi-node duplication.
/// </summary>
public sealed class EmailRetryRecurringJob
{
    private readonly IEmailRetryService _emailRetryService;
    private readonly IDistributedJobCoordinator _coordinator;
    private readonly EmailRetryJobOptions _options;
    private readonly ILogger<EmailRetryRecurringJob> _logger;

    public EmailRetryRecurringJob(
        IEmailRetryService emailRetryService,
        IDistributedJobCoordinator coordinator,
        IOptions<BackgroundJobsOptions> options,
        ILogger<EmailRetryRecurringJob> logger
    )
    {
        _emailRetryService = emailRetryService;
        _coordinator = coordinator;
        _options = options.Value.EmailRetry;
        _logger = logger;
    }

    /// <summary>
    /// TickerQ entry-point that acquires the distributed leader lease and runs retry and
    /// dead-letter operations using settings from <see cref="EmailRetryJobOptions"/>.
    /// </summary>
    [TickerFunction("email-retry-recurring-job")]
    public Task ExecuteAsync(TickerFunctionContext context, CancellationToken ct) =>
        _coordinator.ExecuteIfLeaderAsync(
            "email-retry-recurring-job",
            async token =>
            {
                _logger.ExecutingEmailRetryRecurringJob(context.Id);

                await _emailRetryService.RetryFailedEmailsAsync(
                    _options.MaxRetryAttempts,
                    _options.BatchSize,
                    token
                );
                await _emailRetryService.DeadLetterExpiredAsync(
                    _options.DeadLetterAfterHours,
                    _options.BatchSize,
                    token
                );
            },
            ct
        );
}
