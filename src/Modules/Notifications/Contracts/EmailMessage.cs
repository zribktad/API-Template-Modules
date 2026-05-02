using SharedKernel.Infrastructure.Logging;

namespace Notifications.Contracts;

/// <summary>
///     Immutable message representing a single outbound email dispatched through Wolverine.
/// </summary>
/// <param name="To">
///     Recipient email address (Personally Identifiable Information).
/// </param>
/// <param name="Subject">
///     Email subject.
/// </param>
/// <param name="HtmlBody">
///     Email body content.
/// </param>
/// <param name="TemplateName">
///     Optional template name used for logging and dead-letter categorisation.
/// </param>
/// <param name="Retryable">
///     When <c>true</c> the email retry service will attempt redelivery on failure.
/// </param>
public sealed record EmailMessage(
    [property: PersonalData] string To,
    string Subject,
    string HtmlBody,
    string? TemplateName = null,
    bool Retryable = false
);
