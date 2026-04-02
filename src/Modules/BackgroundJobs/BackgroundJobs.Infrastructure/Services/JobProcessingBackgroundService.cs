using System.Text.Json;
using BackgroundJobs.Application.Services;
using BackgroundJobs.Domain;
using Contracts.Commands.Webhooks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SharedKernel.Domain.Interfaces;
using SharedKernel.Infrastructure.BackgroundJobs.Services;
using Wolverine;

namespace BackgroundJobs.Infrastructure.Services;

/// <summary>
/// Hosted background service that dequeues job IDs from <see cref="IJobQueueReader"/>, simulates
/// multi-step processing with progress updates, and dispatches webhook callbacks on completion or failure.
/// </summary>
public sealed class JobProcessingBackgroundService : QueueConsumerBackgroundService<Guid>
{
    private const int SimulatedStepCount = 5;
    private const int SimulatedStepDelayMs = 200;
    private const int ProgressPerStep = 20;
    private const string CompletedResultSummary = "Job completed successfully";

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<JobProcessingBackgroundService> _logger;
    private readonly TimeProvider _timeProvider;

    public JobProcessingBackgroundService(
        IJobQueueReader queue,
        IServiceScopeFactory scopeFactory,
        ILogger<JobProcessingBackgroundService> logger,
        TimeProvider timeProvider
    )
        : base(queue)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _timeProvider = timeProvider;
    }

    protected override async Task ProcessItemAsync(Guid jobId, CancellationToken ct)
    {
        await using AsyncServiceScope scope = _scopeFactory.CreateAsyncScope();
        IJobExecutionRepository repo =
            scope.ServiceProvider.GetRequiredService<IJobExecutionRepository>();
        IUnitOfWork uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

        JobExecution? job = await repo.GetByIdAsync(jobId, ct);
        if (job is null)
            return;

        job.MarkProcessing(_timeProvider);
        await uow.CommitAsync(ct);

        for (int step = 1; step <= SimulatedStepCount; step++)
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

        await SendCallbackAsync(job, ct);
    }

    protected override async Task HandleErrorAsync(Guid jobId, Exception ex, CancellationToken ct)
    {
        _logger.LogError(ex, "Job {JobId} failed", jobId);
        await TryMarkFailedAsync(jobId, ex.Message, ct);
    }

    private async Task TryMarkFailedAsync(Guid jobId, string errorMessage, CancellationToken ct)
    {
        try
        {
            using CancellationTokenSource timeoutCts = new(TimeSpan.FromSeconds(30));
            using CancellationTokenSource linkedCts =
                CancellationTokenSource.CreateLinkedTokenSource(ct, timeoutCts.Token);
            CancellationToken token = linkedCts.Token;

            await using AsyncServiceScope scope = _scopeFactory.CreateAsyncScope();
            IJobExecutionRepository repo =
                scope.ServiceProvider.GetRequiredService<IJobExecutionRepository>();
            IUnitOfWork uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

            JobExecution? job = await repo.GetByIdAsync(jobId, token);
            if (job is not null)
            {
                job.MarkFailed(errorMessage, _timeProvider);
                await uow.CommitAsync(token);
                await SendCallbackAsync(job, token);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to mark job {JobId} as failed", jobId);
        }
    }

    private async Task SendCallbackAsync(JobExecution job, CancellationToken ct)
    {
        if (job.CallbackUrl is null)
            return;

        string serializedPayload = JsonSerializer.Serialize(
            new
            {
                job.Id,
                job.JobType,
                Status = job.Status.ToString(),
                job.ResultPayload,
                job.ErrorMessage,
                CompletedAtUtc = job.CompletedAtUtc ?? _timeProvider.GetUtcNow().UtcDateTime,
            },
            JsonSerializerOptions.Web
        );

        await using AsyncServiceScope scope = _scopeFactory.CreateAsyncScope();
        IMessageBus messageBus = scope.ServiceProvider.GetRequiredService<IMessageBus>();
        await messageBus.SendAsync(
            new SendWebhookCallbackCommand(job.CallbackUrl, serializedPayload)
        );
    }
}
