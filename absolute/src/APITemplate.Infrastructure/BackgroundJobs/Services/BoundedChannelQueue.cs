using System.Threading.Channels;

namespace APITemplate.Infrastructure.BackgroundJobs.Services;

/// <summary>
/// A generic bounded channel-based queue. Subclass or instantiate directly for
/// specific queue types (jobs, webhooks, emails, etc.).
/// </summary>
public class BoundedChannelQueue<T>
{
    private readonly Channel<T> _channel;

    /// <summary>Creates a bounded channel with the specified <paramref name="capacity"/>, waiting on enqueue when full and using a single reader.</summary>
    public BoundedChannelQueue(int capacity)
    {
        _channel = Channel.CreateBounded<T>(
            new BoundedChannelOptions(capacity)
            {
                FullMode = BoundedChannelFullMode.Wait,
                SingleReader = true,
            }
        );
    }

    /// <summary>Returns an async stream that yields items as they are enqueued, completing when the channel is closed.</summary>
    public IAsyncEnumerable<T> ReadAllAsync(CancellationToken ct = default) =>
        _channel.Reader.ReadAllAsync(ct);

    /// <summary>Writes <paramref name="item"/> to the channel, waiting asynchronously if the channel is at capacity.</summary>
    public ValueTask EnqueueAsync(T item, CancellationToken ct = default) =>
        _channel.Writer.WriteAsync(item, ct);
}
