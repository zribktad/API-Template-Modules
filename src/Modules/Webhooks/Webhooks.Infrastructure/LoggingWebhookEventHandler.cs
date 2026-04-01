using Microsoft.Extensions.Logging;
using Webhooks.Application.Contracts;
using Webhooks.Application.DTOs;

namespace Webhooks.Infrastructure;

public sealed class LoggingWebhookEventHandler : IWebhookEventHandler
{
    private readonly ILogger<LoggingWebhookEventHandler> _logger;

    public LoggingWebhookEventHandler(ILogger<LoggingWebhookEventHandler> logger)
    {
        _logger = logger;
    }

    public string EventType => "*";

    public Task HandleAsync(WebhookPayload payload, CancellationToken ct = default)
    {
        _logger.LogInformation(
            "Received webhook: Type={EventType}, Id={EventId}",
            payload.EventType,
            payload.EventId
        );
        return Task.CompletedTask;
    }
}
