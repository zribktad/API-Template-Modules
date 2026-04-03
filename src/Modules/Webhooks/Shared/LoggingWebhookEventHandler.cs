using Microsoft.Extensions.Logging;
using Webhooks.Shared;
using Webhooks.Logging;

namespace Webhooks.Shared;

public sealed class LoggingWebhookEventHandler : IWebhookEventHandler
{
    private readonly ILogger<LoggingWebhookEventHandler> _logger;

    public LoggingWebhookEventHandler(ILogger<LoggingWebhookEventHandler> logger)
    {
        _logger = logger;
    }

    public string EventType => WebhookConstants.WildcardEventType;

    public Task HandleAsync(WebhookPayload payload, CancellationToken ct = default)
    {
        _logger.WebhookReceived(payload.EventType, payload.EventId);
        return Task.CompletedTask;
    }
}

