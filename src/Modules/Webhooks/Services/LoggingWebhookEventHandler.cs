using Microsoft.Extensions.Logging;
using Webhooks.Contracts;
using Webhooks.Services;
using Webhooks.Security;
using Webhooks.Logging;

namespace Webhooks.Services;

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




