namespace Notifications.Contracts;

/// <summary>
///     Immutable message representing a single outbound email dispatched through Wolverine.
/// </summary>
/// <param name="TemplateName">
///     Optional template name used for logging and dead-letter categorisation.
/// </param>
/// <param name="Retryable">
///     When <c>true</c> the email retry service will attempt redelivery on failure.
/// </param>
public sealed record EmailMessage(
    string To,
    string Subject,
    string HtmlBody,
    string? TemplateName = null,
    bool Retryable = false
);
