using BuildingBlocks.Web.Logging;
using Microsoft.Extensions.Logging;

namespace Webhooks.Logging;

/// <summary>
///     Source-generated logger extension methods for Webhooks infrastructure diagnostics.
/// </summary>
internal static partial class WebhooksInfrastructureLogs
{
    // LoggingWebhookEventHandler (7001)
    [LoggerMessage(
        EventId = 7001,
        Level = LogLevel.Information,
        Message = "Received webhook: Type={EventType}, Id={EventId}"
    )]
    public static partial void WebhookReceived(
        this ILogger logger,
        string eventType,
        string eventId
    );

    // OutgoingWebhookBackgroundService (7002, 7003)
    [LoggerMessage(
        EventId = 7002,
        Level = LogLevel.Information,
        Message = "Outgoing webhook delivered to {Url}"
    )]
    public static partial void OutgoingWebhookDelivered(
        this ILogger logger,
        [SensitiveData] string url
    );

    [LoggerMessage(
        EventId = 7003,
        Level = LogLevel.Error,
        Message = "Failed to deliver outgoing webhook to {Url}"
    )]
    public static partial void OutgoingWebhookDeliveryFailed(
        this ILogger logger,
        Exception exception,
        [SensitiveData] string url
    );

    // WebhookProcessingBackgroundService (7004, 7005)
    [LoggerMessage(
        EventId = 7004,
        Level = LogLevel.Warning,
        Message = "No handler registered for webhook event type '{EventType}' (Id={EventId})"
    )]
    public static partial void WebhookNoHandlerRegistered(
        this ILogger logger,
        string eventType,
        string eventId
    );

    [LoggerMessage(
        EventId = 7005,
        Level = LogLevel.Error,
        Message = "Failed to process webhook: Type={EventType}, Id={EventId}"
    )]
    public static partial void WebhookProcessingFailed(
        this ILogger logger,
        Exception exception,
        string eventType,
        string eventId
    );
}
