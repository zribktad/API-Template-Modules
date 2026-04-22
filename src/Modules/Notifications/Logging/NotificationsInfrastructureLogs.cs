using Microsoft.Extensions.Logging;
using SharedKernel.Infrastructure.Logging;

namespace Notifications.Logging;

/// <summary>
///     Source-generated logger extension methods for Notifications infrastructure diagnostics.
/// </summary>
internal static partial class NotificationsInfrastructureLogs
{
    // Email notification handlers (6000)
    [LoggerMessage(
        EventId = 6000,
        Level = LogLevel.Error,
        Message = "Failed to render email template '{TemplateName}': {ErrorCode} — {Description}. Event processing will fail and follow the configured Wolverine error flow."
    )]
    public static partial void EmailTemplateRenderFailed(
        this ILogger logger,
        string templateName,
        string errorCode,
        string description
    );

    // EmailRetryRecurringJob (6001)
    [LoggerMessage(
        EventId = 6001,
        Level = LogLevel.Information,
        Message = "Executing email retry recurring job for ticker {TickerId}."
    )]
    public static partial void ExecutingEmailRetryRecurringJob(this ILogger logger, Guid tickerId);

    // EmailRetryService (6002, 6003, 6004)
    [LoggerMessage(
        EventId = 6002,
        Level = LogLevel.Information,
        Message = "Successfully retried email to {Recipient} (attempt {Attempt})."
    )]
    public static partial void EmailRetrySucceeded(
        this ILogger logger,
        [PersonalData] string recipient,
        int attempt
    );

    [LoggerMessage(
        EventId = 6003,
        Level = LogLevel.Warning,
        Message = "Retry attempt {Attempt} failed for email to {Recipient}."
    )]
    public static partial void EmailRetryAttemptFailed(
        this ILogger logger,
        Exception exception,
        int attempt,
        [PersonalData] string recipient
    );

    [LoggerMessage(
        EventId = 6004,
        Level = LogLevel.Warning,
        Message = "Dead-lettered email to {Recipient} with subject '{Subject}' after {Hours}h."
    )]
    public static partial void EmailDeadLettered(
        this ILogger logger,
        [PersonalData] string recipient,
        string subject,
        int hours
    );

    [LoggerMessage(
        EventId = 6009,
        Level = LogLevel.Warning,
        Message = "Optimistic concurrency conflict while committing retry progress for email to {Recipient}; another worker may have updated the row."
    )]
    public static partial void EmailRetryCommitConcurrencyConflict(
        this ILogger logger,
        [PersonalData] string recipient
    );

    [LoggerMessage(
        EventId = 6010,
        Level = LogLevel.Warning,
        Message = "Optimistic concurrency conflict after successful send while deleting failed-email row for {Recipient}; row still exists — verify for possible duplicate delivery."
    )]
    public static partial void EmailRetryDeleteConcurrencyAfterSend(
        this ILogger logger,
        [PersonalData] string recipient
    );

    [LoggerMessage(
        EventId = 6011,
        Level = LogLevel.Warning,
        Message = "Optimistic concurrency conflict while committing dead-letter batch; another worker may have updated one or more rows."
    )]
    public static partial void EmailDeadLetterCommitConcurrencyConflict(this ILogger logger);

    // SendEmailMessageHandler (6005)
    [LoggerMessage(
        EventId = 6005,
        Level = LogLevel.Error,
        Message = "Failed to send email to {Recipient} with subject '{Subject}' after all retry attempts."
    )]
    public static partial void EmailSendFailed(
        this ILogger logger,
        Exception exception,
        [PersonalData] string recipient,
        string subject
    );

    // FailedEmailStore (6006, 6007)
    [LoggerMessage(
        EventId = 6006,
        Level = LogLevel.Warning,
        Message = "Stored failed email to {Recipient} with subject '{Subject}' for retry."
    )]
    public static partial void FailedEmailStored(
        this ILogger logger,
        [PersonalData] string recipient,
        string subject
    );

    [LoggerMessage(
        EventId = 6007,
        Level = LogLevel.Error,
        Message = "Failed to store failed email to {Recipient} for retry."
    )]
    public static partial void FailedEmailStorageError(
        this ILogger logger,
        Exception exception,
        [PersonalData] string recipient
    );

    // MailKitEmailSender (6008, 6012)
    [LoggerMessage(
        EventId = 6008,
        Level = LogLevel.Information,
        Message = "Email sent to {Recipient} with subject '{Subject}'."
    )]
    public static partial void EmailSent(
        this ILogger logger,
        [PersonalData] string recipient,
        string subject
    );

    [LoggerMessage(
        EventId = 6012,
        Level = LogLevel.Error,
        Message = "SMTP transmission failed for recipient {Recipient}."
    )]
    public static partial void SmtpSendFailed(
        this ILogger logger,
        Exception exception,
        [PersonalData] string recipient
    );
}
