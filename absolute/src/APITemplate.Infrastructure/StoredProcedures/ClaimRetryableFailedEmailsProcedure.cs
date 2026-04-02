using APITemplate.Domain.Entities;
using APITemplate.Domain.Interfaces;

namespace APITemplate.Infrastructure.StoredProcedures;

/// <summary>
/// Calls the <c>claim_retryable_failed_emails(...)</c> PostgreSQL function.
/// Atomically selects and claims a batch of retryable failed emails using
/// <c>FOR UPDATE SKIP LOCKED</c> to avoid contention between concurrent workers.
/// </summary>
public sealed record ClaimRetryableFailedEmailsProcedure(
    int MaxRetryAttempts,
    int BatchSize,
    string ClaimedBy,
    DateTime ClaimedAtUtc,
    DateTime ClaimedUntilUtc
) : IStoredProcedure<FailedEmail>
{
    public FormattableString ToSql() =>
        $"SELECT * FROM claim_retryable_failed_emails({MaxRetryAttempts}, {BatchSize}, {ClaimedBy}, {ClaimedAtUtc}, {ClaimedUntilUtc})";
}
