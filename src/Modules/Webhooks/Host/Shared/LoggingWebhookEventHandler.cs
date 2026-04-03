using Microsoft.Extensions.Logging;
using Webhooks.Core.Shared;
using Webhooks.Host.Logging;

namespace Webhooks.Host.Shared;

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
