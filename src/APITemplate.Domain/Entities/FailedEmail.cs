namespace APITemplate.Domain.Entities;

/// <summary>
/// Represents an email that could not be delivered and is queued for retry.
/// Supports pessimistic concurrency via claim fields to prevent duplicate processing across workers.
/// </summary>
public sealed class FailedEmail : IHasId
{
    /// <summary>Maximum character length stored for the <see cref="LastError"/> field.</summary>
    public const int LastErrorMaxLength = 2000;

    public Guid Id { get; set; }
    public required string To { get; set; }
    public required string Subject { get; set; }
    public required string HtmlBody { get; set; }
    public int RetryCount { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? LastAttemptAtUtc { get; set; }
    public string? LastError { get; set; }
    public string? TemplateName { get; set; }
    public bool IsDeadLettered { get; set; }
    public string? ClaimedBy { get; set; }
    public DateTime? ClaimedAtUtc { get; set; }
    public DateTime? ClaimedUntilUtc { get; set; }
}
