using Microsoft.Extensions.Logging;
using SharedKernel.Infrastructure.Logging;

namespace Notifications.Logging;

/// <summary>
/// Source-generated logger extension methods for Notifications infrastructure diagnostics.
/// </summary>
internal static partial class NotificationsInfrastructureLogs
{
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

    // EmailSendingBackgroundService (6005)
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

    // MailKitEmailSender (6008)
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
}


