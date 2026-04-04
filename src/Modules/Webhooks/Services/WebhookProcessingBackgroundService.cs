using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SharedKernel.Infrastructure.BackgroundJobs.Services;
using Webhooks.Contracts;
using Webhooks.Logging;

namespace Webhooks.Services;

public sealed class WebhookProcessingBackgroundService
    : QueueConsumerBackgroundService<WebhookPayload>
{
    private readonly ILogger<WebhookProcessingBackgroundService> _logger;
    private readonly IServiceScopeFactory _scopeFactory;

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
        IEnumerable<IWebhookEventHandler> handlers = scope.ServiceProvider.GetRequiredService<
            IEnumerable<IWebhookEventHandler>
        >();

        bool handled = false;
        foreach (IWebhookEventHandler handler in handlers)
        {
            if (
                handler.EventType == WebhookConstants.WildcardEventType
                || handler.EventType == payload.EventType
            )
            {
                await handler.HandleAsync(payload, ct);
                handled = true;
            }
        }

        if (!handled)
            _logger.WebhookNoHandlerRegistered(payload.EventType, payload.EventId);
    }

    protected override Task HandleErrorAsync(
        WebhookPayload payload,
        Exception ex,
        CancellationToken ct
    )
    {
        _logger.WebhookProcessingFailed(ex, payload.EventType, payload.EventId);
        return Task.CompletedTask;
    }
}
