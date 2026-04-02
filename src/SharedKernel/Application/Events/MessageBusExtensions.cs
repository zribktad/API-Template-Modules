using Microsoft.Extensions.Logging;
using Wolverine;

namespace SharedKernel.Application.Events;

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

/// <summary>
/// Shared <see cref="OutgoingMessages"/> instances for use in handler error-path returns.
/// </summary>
/// <remarks>
/// <see cref="Empty"/> is a cached empty instance. Do not call <c>Add</c> on it —
/// it is only safe to use when returned immediately without mutation.
/// </remarks>
public static class OutgoingMessagesHelper
{
    /// <summary>
    /// A shared empty <see cref="OutgoingMessages"/> for error-path returns.
    /// Return this instead of <c>new OutgoingMessages()</c> when no messages need to be dispatched.
    /// </summary>
    public static readonly OutgoingMessages Empty = new();
}
