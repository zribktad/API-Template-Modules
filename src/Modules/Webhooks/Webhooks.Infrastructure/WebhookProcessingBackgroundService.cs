using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SharedKernel.Infrastructure.BackgroundJobs.Services;
using Webhooks.Application.Contracts;
using Webhooks.Application.DTOs;

namespace Webhooks.Infrastructure;

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

    protected override async Task ProcessItemAsync(WebhookPayload payload, CancellationToken ct)
    {
        await using AsyncServiceScope scope = _scopeFactory.CreateAsyncScope();
        IEnumerable<IWebhookEventHandler> handlers = scope.ServiceProvider
            .GetRequiredService<IEnumerable<IWebhookEventHandler>>();

        bool handled = false;
        foreach (IWebhookEventHandler handler in handlers)
        {
            if (handler.EventType == WebhookConstants.WildcardEventType || handler.EventType == payload.EventType)
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

    protected override Task HandleErrorAsync(WebhookPayload payload, Exception ex, CancellationToken ct)
    {
        _logger.LogError(ex,
            "Failed to process webhook: Type={EventType}, Id={EventId}",
            payload.EventType, payload.EventId);
        return Task.CompletedTask;
    }
}
