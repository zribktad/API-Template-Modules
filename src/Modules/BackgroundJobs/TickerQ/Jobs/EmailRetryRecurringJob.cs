using BackgroundJobs.Logging;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Notifications.Contracts;
using TickerQ.Utilities.Base;

namespace BackgroundJobs.TickerQ.Jobs;

public sealed class EmailRetryRecurringJob
{
    private readonly IDistributedJobCoordinator _coordinator;
    private readonly IEmailRetryService _emailRetryService;
    private readonly ILogger<EmailRetryRecurringJob> _logger;
    private readonly EmailRetryJobOptions _options;

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

    [TickerFunction("email-retry-recurring-job")]
    public Task ExecuteAsync(TickerFunctionContext context, CancellationToken ct)
    {
        return _coordinator.ExecuteIfLeaderAsync(
            "email-retry-recurring-job",
            async token =>
            {
                _logger.ExecutingEmailRetryRecurringJob(context.Id);

                await _emailRetryService.RetryFailedEmailsAsync(
                    _options.MaxRetryAttempts,
                    _options.BatchSize,
                    _options.ClaimLeaseMinutes,
                    token
                );
                await _emailRetryService.DeadLetterExpiredAsync(
                    _options.DeadLetterAfterHours,
                    _options.BatchSize,
                    _options.ClaimLeaseMinutes,
                    token
                );
            },
            ct
        );
    }
}