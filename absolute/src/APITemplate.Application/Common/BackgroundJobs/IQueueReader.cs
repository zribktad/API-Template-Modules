namespace APITemplate.Application.Common.BackgroundJobs;

/// <summary>
/// Generic read-side abstraction for in-process queues, allowing background consumers to drain
/// items without coupling to a specific transport implementation.
/// </summary>
/// <typeparam name="T">The type of item read from the queue.</typeparam>
public interface IQueueReader<out T>
{
    /// <summary>
    /// Returns an async stream that yields items as they become available, completing only when
    /// <paramref name="ct"/> is cancelled or the underlying channel is closed.
    /// </summary>
    IAsyncEnumerable<T> ReadAllAsync(CancellationToken ct = default);
}
