using Microsoft.Extensions.Logging;
using Wolverine;

namespace APITemplate.Application.Common.Events;

/// <summary>
/// Extension methods for <see cref="IMessageBus"/> providing safe (fire-and-forget) publishing.
/// </summary>
public static class MessageBusExtensions
{
    /// <summary>
    /// Publishes a message, swallowing any non-cancellation exception and logging it as a warning.
    /// Use for notification events whose failure must not break the main command flow.
    /// </summary>
    public static async Task PublishSafeAsync<TEvent>(
        this IMessageBus bus,
        TEvent @event,
        ILogger logger
    )
    {
        try
        {
            await bus.PublishAsync(@event);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Failed to publish {EventType}.", typeof(TEvent).Name);
        }
    }
}
