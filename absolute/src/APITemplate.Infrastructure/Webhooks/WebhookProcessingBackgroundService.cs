using APITemplate.Application.Common.BackgroundJobs;
using APITemplate.Application.Common.Contracts;
using APITemplate.Application.Features.Examples.DTOs;
using APITemplate.Infrastructure.BackgroundJobs.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace APITemplate.Infrastructure.Webhooks;

/// <summary>
/// Background service that drains the incoming webhook processing queue, dispatching each
/// payload to all registered <see cref="IWebhookEventHandler"/> implementations that match
/// the event type or are registered as catch-all ("*") handlers.
/// </summary>
public sealed class WebhookProcessingBackgroundService
    : QueueConsumerBackgroundService<WebhookPayload>
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<WebhookProcessingBackgroundService> _logger;

    public WebhookProcessingBackgroundService(
        IWebhookQueueReader queue,
        IServiceScopeFactory scopeFactory,
        ILogger<WebhookProcessingBackgroundService> logger
    )
        : base(queue)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    /// <summary>
    /// Resolves all registered handlers in a fresh DI scope and invokes those whose event type
    /// matches the payload or is the wildcard "*". Logs a warning when no handler is matched.
    /// </summary>
    protected override async Task ProcessItemAsync(WebhookPayload payload, CancellationToken ct)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var handlers = scope.ServiceProvider.GetRequiredService<
            IEnumerable<IWebhookEventHandler>
        >();

        var handled = false;
        foreach (var handler in handlers)
        {
            if (handler.EventType == "*" || handler.EventType == payload.EventType)
            {
                await handler.HandleAsync(payload, ct);
                handled = true;
            }
        }

        if (!handled)
        {
            _logger.LogWarning(
                "No handler registered for webhook event type '{EventType}' (Id={EventId})",
                payload.EventType,
                payload.EventId
            );
        }
    }

    /// <summary>Logs processing failures at error level and returns a completed task to allow the queue to continue.</summary>
    protected override Task HandleErrorAsync(
        WebhookPayload payload,
        Exception ex,
        CancellationToken ct
    )
    {
        _logger.LogError(
            ex,
            "Failed to process webhook: Type={EventType}, Id={EventId}",
            payload.EventType,
            payload.EventId
        );
        return Task.CompletedTask;
    }
}
