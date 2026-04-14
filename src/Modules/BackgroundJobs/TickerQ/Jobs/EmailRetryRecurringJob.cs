using BackgroundJobs.Domain;
using BackgroundJobs.Logging;
using Microsoft.Extensions.Logging;
using TickerQ.Utilities.Base;

namespace BackgroundJobs.TickerQ.Jobs;

public sealed class EmailRetryRecurringJob
{
    private readonly IDistributedJobCoordinator _coordinator;
    private readonly IEmailRetryJobService _emailRetryJobService;
    private readonly ILogger<EmailRetryRecurringJob> _logger;

    public EmailRetryRecurringJob(
        IEmailRetryJobService emailRetryJobService,
        IDistributedJobCoordinator coordinator,
        ILogger<EmailRetryRecurringJob> logger
    )
    {
        _emailRetryJobService = emailRetryJobService;
        _coordinator = coordinator;
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

                await _emailRetryJobService.RetryFailedEmailsAsync(token);
                await _emailRetryJobService.DeadLetterExpiredAsync(token);
            },
            ct
        );
    }
}