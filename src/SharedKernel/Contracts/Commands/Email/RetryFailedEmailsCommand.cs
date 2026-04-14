namespace SharedKernel.Contracts.Commands.Email;

/// <summary>
///     Cross-module command instructing the Notifications module to re-attempt delivery
///     of previously failed emails. Dispatched by the BackgroundJobs email-retry orchestrator
///     via the message bus.
/// </summary>
public sealed record RetryFailedEmailsCommand(
    int MaxRetryAttempts,
    int BatchSize,
    int ClaimLeaseMinutes
);
