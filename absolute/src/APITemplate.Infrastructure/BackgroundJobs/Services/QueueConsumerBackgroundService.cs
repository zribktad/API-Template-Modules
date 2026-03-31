using APITemplate.Application.Common.BackgroundJobs;
using Microsoft.Extensions.Hosting;

namespace APITemplate.Infrastructure.BackgroundJobs.Services;

/// <summary>
/// Base <see cref="BackgroundService"/> that drains an <see cref="IQueueReader{T}"/> in a
/// continuous async loop, dispatching each item to <see cref="ProcessItemAsync"/> and routing
/// non-cancellation exceptions to <see cref="HandleErrorAsync"/>.
/// </summary>
public abstract class QueueConsumerBackgroundService<T> : BackgroundService
{
    private readonly IQueueReader<T> _queue;

    protected QueueConsumerBackgroundService(IQueueReader<T> queue) => _queue = queue;

    protected sealed override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var item in _queue.ReadAllAsync(stoppingToken))
        {
            try
            {
                await ProcessItemAsync(item, stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                await HandleErrorAsync(item, ex, stoppingToken);
            }
        }
    }

    /// <summary>Processes a single dequeued item; implement the core business logic here.</summary>
    protected abstract Task ProcessItemAsync(T item, CancellationToken ct);

    /// <summary>Called when <see cref="ProcessItemAsync"/> throws a non-cancellation exception; default implementation is a no-op.</summary>
    protected virtual Task HandleErrorAsync(T item, Exception ex, CancellationToken ct) =>
        Task.CompletedTask;
}
