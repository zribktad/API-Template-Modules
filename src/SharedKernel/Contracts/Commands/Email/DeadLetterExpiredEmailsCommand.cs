namespace SharedKernel.Contracts.Commands.Email;

/// <summary>
///     Cross-module command instructing the Notifications module to move emails that have
///     exceeded the age threshold to a dead-letter store.
///     Dispatched by the BackgroundJobs email-retry orchestrator via the message bus.
/// </summary>
public sealed record DeadLetterExpiredEmailsCommand(
    int DeadLetterAfterHours,
    int BatchSize,
    int ClaimLeaseMinutes
);
