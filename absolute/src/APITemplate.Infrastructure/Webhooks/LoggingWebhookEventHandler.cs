using APITemplate.Application.Common.Contracts;
using APITemplate.Application.Features.Examples.DTOs;
using Microsoft.Extensions.Logging;

namespace APITemplate.Infrastructure.Webhooks;

/// <summary>
/// A catch-all <see cref="IWebhookEventHandler"/> (EventType = "*") that logs all received
/// webhook events, primarily useful for debugging and as a no-op example handler.
/// </summary>
public sealed class LoggingWebhookEventHandler : IWebhookEventHandler
{
    private readonly ILogger<LoggingWebhookEventHandler> _logger;

    public LoggingWebhookEventHandler(ILogger<LoggingWebhookEventHandler> logger)
    {
        _logger = logger;
    }

    public string EventType => "*";

    /// <summary>Logs the event type and ID at information level and returns a completed task.</summary>
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
