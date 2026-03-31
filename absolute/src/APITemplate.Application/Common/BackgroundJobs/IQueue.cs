namespace APITemplate.Application.Common.BackgroundJobs;

/// <summary>
/// Generic write-side abstraction for in-process queues used to decouple producers from
/// background consumers without taking a dependency on a specific transport (e.g. Channel, Redis).
/// </summary>
/// <typeparam name="T">The type of item placed on the queue.</typeparam>
public interface IQueue<in T>
{
    /// <summary>
    /// Adds <paramref name="item"/> to the queue, waiting asynchronously if the queue is full.
    /// </summary>
    ValueTask EnqueueAsync(T item, CancellationToken ct = default);
}
