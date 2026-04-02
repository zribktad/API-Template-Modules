using System.Text.Json;
using APITemplate.Application.Common.BackgroundJobs;
using APITemplate.Application.Features.Examples.DTOs;
using APITemplate.Domain.Entities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace APITemplate.Infrastructure.BackgroundJobs.Services;

/// <summary>
/// Hosted background service that dequeues job IDs from <see cref="IJobQueueReader"/>, simulates
/// multi-step processing with progress updates, and dispatches webhook callbacks on completion or failure.
/// Each job is processed in its own DI scope to ensure repository and unit-of-work isolation.
/// </summary>
public sealed class JobProcessingBackgroundService : QueueConsumerBackgroundService<Guid>
{
    private const int SimulatedStepCount = 5;
    private const int SimulatedStepDelayMs = 200;
    private const int ProgressPerStep = 20;
    private const string CompletedResultSummary = "Job completed successfully";

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IOutgoingWebhookQueue _outgoingWebhookQueue;
    private readonly ILogger<JobProcessingBackgroundService> _logger;
    private readonly TimeProvider _timeProvider;

    public JobProcessingBackgroundService(
        IJobQueueReader queue,
        IServiceScopeFactory scopeFactory,
        IOutgoingWebhookQueue outgoingWebhookQueue,
        ILogger<JobProcessingBackgroundService> logger,
        TimeProvider timeProvider
    )
        : base(queue)
    {
        _scopeFactory = scopeFactory;
        _outgoingWebhookQueue = outgoingWebhookQueue;
        _logger = logger;
        _timeProvider = timeProvider;
    }

    /// <summary>
    /// Marks the job as processing, simulates five incremental progress steps, marks it
    /// completed with a result payload, and enqueues a webhook callback if a callback URL is configured.
    /// </summary>
    protected override async Task ProcessItemAsync(Guid jobId, CancellationToken ct)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var repo = scope.ServiceProvider.GetRequiredService<IJobExecutionRepository>();
        var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

        var job = await repo.GetByIdAsync(jobId, ct);
        if (job is null)
            return;

        job.MarkProcessing(_timeProvider);
        await uow.CommitAsync(ct);

        for (var step = 1; step <= SimulatedStepCount; step++)
        {
            await Task.Delay(SimulatedStepDelayMs, ct);
            job.UpdateProgress(step * ProgressPerStep);
            await uow.CommitAsync(ct);
        }

        job.MarkCompleted(
            JsonSerializer.Serialize(new { summary = CompletedResultSummary }),
            _timeProvider
        );
        await uow.CommitAsync(ct);

        await EnqueueCallbackAsync(job, ct);
    }

    /// <summary>Logs the error and attempts to persist the failed state and enqueue a failure callback within a 30-second timeout.</summary>
    protected override async Task HandleErrorAsync(Guid jobId, Exception ex, CancellationToken ct)
    {
        _logger.LogError(ex, "Job {JobId} failed", jobId);
        await TryMarkFailedAsync(jobId, ex.Message, ct);
    }

    private async Task TryMarkFailedAsync(Guid jobId, string errorMessage, CancellationToken ct)
    {
        try
        {
            using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
                ct,
                timeoutCts.Token
            );
            var token = linkedCts.Token;

            await using var scope = _scopeFactory.CreateAsyncScope();
            var repo = scope.ServiceProvider.GetRequiredService<IJobExecutionRepository>();
            var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

            var job = await repo.GetByIdAsync(jobId, token);
            if (job is not null)
            {
                job.MarkFailed(errorMessage, _timeProvider);
                await uow.CommitAsync(token);

                await EnqueueCallbackAsync(job, token);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to mark job {JobId} as failed", jobId);
        }
    }

    /// <summary>Serialises the job outcome into an <see cref="OutgoingWebhookItem"/> and pushes it to the outgoing webhook queue when a callback URL is present.</summary>
    private async Task EnqueueCallbackAsync(JobExecution job, CancellationToken ct)
    {
        if (job.CallbackUrl is null)
            return;

        var payload = new OutgoingJobWebhookPayload(
            job.Id,
            job.JobType,
            job.Status.ToString(),
            job.ResultPayload,
            job.ErrorMessage,
            job.CompletedAtUtc ?? _timeProvider.GetUtcNow().UtcDateTime
        );

        var serialized = JsonSerializer.Serialize(payload, JsonSerializerOptions.Web);
        var item = new OutgoingWebhookItem(job.CallbackUrl, serialized);

        await _outgoingWebhookQueue.EnqueueAsync(item, ct);
    }
}
