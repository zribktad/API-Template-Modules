using System.Text.Json;
using BackgroundJobs.Logging;
using BuildingBlocks.Web.InfrastructureBackgroundJobs.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SharedKernel.Contracts.Commands.Webhooks;
using Wolverine;

namespace BackgroundJobs.Services;

/// <summary>
///     Hosted background service that dequeues job IDs from <see cref="IJobQueueReader" />, simulates
///     multi-step processing with progress updates, and dispatches webhook callbacks on completion or failure.
/// </summary>
public sealed class JobProcessingBackgroundService : QueueConsumerBackgroundService<Guid>
{
    private const int SimulatedStepCount = 5;
    private const int SimulatedStepDelayMs = 200;
    private const int ProgressPerStep = 20;
    private const string CompletedResultSummary = "Job completed successfully";
    private readonly ILogger<JobProcessingBackgroundService> _logger;

    private readonly IServiceScopeFactory _scopeFactory;
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

        ErrorOr<Success> markProcessingResult = job.MarkProcessing(_timeProvider);
        if (markProcessingResult.IsError)
        {
            _logger.JobAlreadyClaimed(jobId, job.Status);
            return;
        }
        await uow.CommitAsync(ct);

        for (int step = 1; step <= SimulatedStepCount; step++)
        {
            await Task.Delay(SimulatedStepDelayMs, ct);
            job.UpdateProgress(step * ProgressPerStep);
            await uow.CommitAsync(ct);
        }

        ErrorOr<Success> markCompletedResult = job.MarkCompleted(
            JsonSerializer.Serialize(new { summary = CompletedResultSummary }),
            _timeProvider
        );
        if (markCompletedResult.IsError)
        {
            throw new InvalidOperationException(
                $"Unexpected state when completing job {jobId}: {markCompletedResult.FirstError.Description}"
            );
        }

        await uow.CommitAsync(ct);

        await SendCallbackAsync(job, ct);
    }

    protected override async Task HandleErrorAsync(Guid jobId, Exception ex, CancellationToken ct)
    {
        _logger.JobFailed(ex, jobId);
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
            if (job is null)
                return;

            ErrorOr<Success> markResult = job.MarkFailed(errorMessage, _timeProvider);
            if (markResult.IsError)
            {
                _logger.JobAlreadyInTerminalState(jobId, job.Status);
                return;
            }

            await uow.CommitAsync(token);
            await SendCallbackAsync(job, token);
        }
        catch (Exception ex)
        {
            _logger.MarkJobFailedError(ex, jobId);
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
