using ErrorOr;
using SharedKernel.Domain.Entities.Contracts;

namespace Notifications.Domain;

/// <summary>
///     Represents an email that could not be delivered and is queued for retry.
///     Supports pessimistic concurrency via claim fields to prevent duplicate processing across workers.
/// </summary>
public sealed class FailedEmail : IHasId
{
    /// <summary>Maximum character length stored for the <see cref="LastError" /> field.</summary>
    public const int LastErrorMaxLength = 2000;

    public required string To { get; set; }
    public required string Subject { get; set; }
    public required string HtmlBody { get; set; }
    public int RetryCount { get; private set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? LastAttemptAtUtc { get; private set; }
    public string? LastError { get; private set; }
    public string? TemplateName { get; set; }
    public bool IsDeadLettered { get; private set; }
    public string? ClaimedBy { get; private set; }
    public DateTime? ClaimedAtUtc { get; private set; }
    public DateTime? ClaimedUntilUtc { get; private set; }

    public Guid Id { get; set; }

    /// <summary>
    ///     Creates a new <see cref="FailedEmail" /> with a generated id and the current UTC time as <see cref="CreatedAtUtc" />.
    ///     If <paramref name="initialError" /> is provided it is stored as <see cref="LastError" /> (truncated to
    ///     <see cref="LastErrorMaxLength" /> characters) without incrementing <see cref="RetryCount" />.
    /// </summary>
    public static FailedEmail Create(
        string to,
        string subject,
        string htmlBody,
        TimeProvider timeProvider,
        string? templateName = null,
        string? initialError = null
    )
    {
        string? truncatedError = initialError is { Length: > LastErrorMaxLength }
            ? initialError[..LastErrorMaxLength]
            : initialError;

        return new FailedEmail
        {
            Id = Guid.NewGuid(),
            To = to,
            Subject = subject,
            HtmlBody = htmlBody,
            TemplateName = templateName,
            CreatedAtUtc = timeProvider.GetUtcNow().UtcDateTime,
            LastError = truncatedError,
        };
    }

    /// <summary>
    ///     Attempts to claim this email for exclusive processing by <paramref name="workerId" />.
    ///     Returns <see cref="Error.Conflict" /> if a valid (non-expired) claim already exists.
    /// </summary>
    public ErrorOr<Success> Claim(string workerId, TimeProvider timeProvider, TimeSpan leaseDuration)
    {
        DateTime now = timeProvider.GetUtcNow().UtcDateTime;
        if (ClaimedUntilUtc.HasValue && ClaimedUntilUtc.Value > now)
            return Error.Conflict(description: "Email is already claimed by another worker.");

        ClaimedBy = workerId;
        ClaimedAtUtc = now;
        ClaimedUntilUtc = now.Add(leaseDuration);
        return Result.Success;
    }

    /// <summary>
    ///     Increments <see cref="RetryCount" />, records the failure timestamp and message,
    ///     and releases the claim so the email is eligible for future retry attempts.
    ///     The error message is truncated to <see cref="LastErrorMaxLength" /> characters if necessary.
    /// </summary>
    public void RecordFailure(string errorMessage, TimeProvider timeProvider)
    {
        RetryCount++;
        LastAttemptAtUtc = timeProvider.GetUtcNow().UtcDateTime;
        LastError = errorMessage.Length > LastErrorMaxLength
            ? errorMessage[..LastErrorMaxLength]
            : errorMessage;
        ClaimedBy = null;
        ClaimedAtUtc = null;
        ClaimedUntilUtc = null;
    }

    /// <summary>
    ///     Marks this email as permanently undeliverable and releases any active claim.
    /// </summary>
    public void MarkDeadLettered()
    {
        IsDeadLettered = true;
        ClaimedBy = null;
        ClaimedAtUtc = null;
        ClaimedUntilUtc = null;
    }
}
