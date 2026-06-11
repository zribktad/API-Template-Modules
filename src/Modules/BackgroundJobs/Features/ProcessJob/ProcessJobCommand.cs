using System.Text.Json;
using BackgroundJobs.Logging;
using BuildingBlocks.Messaging.Events;
using Microsoft.Extensions.Logging;
using SharedKernel.Contracts.Commands.Webhooks;
using Wolverine;

namespace BackgroundJobs.Features;

/// <summary>
///     Durable Wolverine command that drives processing of a previously submitted <see cref="JobExecution" />.
///     Published by <see cref="SubmitJobCommandHandler" /> and persisted in the durable local queue, so it
///     survives a process restart and is retried/redelivered by Wolverine instead of being lost in an
///     in-memory channel.
/// </summary>
public sealed record ProcessJobCommand(Guid JobId);

public sealed class ProcessJobCommandHandler
{
    private const int SimulatedStepCount = 5;
    private const int SimulatedStepDelayMs = 200;
    private const int ProgressPerStep = 20;
    private const string CompletedResultSummary = "Job completed successfully";

    public static async Task<OutgoingMessages> HandleAsync(
        ProcessJobCommand command,
        IJobExecutionRepository repository,
        IUnitOfWork<BackgroundJobsDbMarker> unitOfWork,
        TimeProvider timeProvider,
        ILogger<ProcessJobCommandHandler> logger,
        CancellationToken ct
    )
    {
        JobExecution? job = await repository.GetByIdAsync(command.JobId, ct);
        if (job is null)
            return OutgoingMessagesHelper.Empty;

        // Idempotent redelivery: a job that already reached a terminal state was fully handled
        // (including its callback) on a previous attempt, so there is nothing left to do.
        if (job.Status is JobStatus.Completed or JobStatus.Failed)
            return OutgoingMessagesHelper.Empty;

        try
        {
            // Pending -> Processing on first delivery. On a redelivery after a mid-flight crash the
            // job is already Processing, so we simply resume the remaining (idempotent) work.
            if (job.Status == JobStatus.Pending)
            {
                ErrorOr<Success> markProcessingResult = job.MarkProcessing(timeProvider);
                if (markProcessingResult.IsError)
                {
                    logger.JobAlreadyClaimed(command.JobId, job.Status);
                    return OutgoingMessagesHelper.Empty;
                }

                await unitOfWork.CommitAsync(ct);
            }

            for (int step = 1; step <= SimulatedStepCount; step++)
            {
                await Task.Delay(SimulatedStepDelayMs, ct);
                job.UpdateProgress(step * ProgressPerStep);
                await unitOfWork.CommitAsync(ct);
            }

            ErrorOr<Success> markCompletedResult = job.MarkCompleted(
                JsonSerializer.Serialize(new { summary = CompletedResultSummary }),
                timeProvider
            );
            if (markCompletedResult.IsError)
            {
                throw new InvalidOperationException(
                    $"Unexpected state when completing job {command.JobId}: {markCompletedResult.FirstError.Description}"
                );
            }

            await unitOfWork.CommitAsync(ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Terminal application failure: mark the job failed and still deliver the failure
            // callback. (A host shutdown surfaces as OperationCanceledException and is intentionally
            // left to Wolverine for redelivery, so the job resumes from Processing on restart.)
            logger.JobFailed(ex, command.JobId);

            if (job.Status == JobStatus.Processing)
            {
                ErrorOr<Success> markFailedResult = job.MarkFailed(ex.Message, timeProvider);
                if (markFailedResult.IsError)
                {
                    logger.JobAlreadyInTerminalState(command.JobId, job.Status);
                    return OutgoingMessagesHelper.Empty;
                }

                await unitOfWork.CommitAsync(ct);
            }
        }

        return BuildCallbackMessages(job, timeProvider);
    }

    private static OutgoingMessages BuildCallbackMessages(
        JobExecution job,
        TimeProvider timeProvider
    )
    {
        if (job.CallbackUrl is null)
            return OutgoingMessagesHelper.Empty;

        string serializedPayload = JsonSerializer.Serialize(
            new
            {
                job.Id,
                job.JobType,
                Status = job.Status.ToString(),
                job.ResultPayload,
                job.ErrorMessage,
                CompletedAtUtc = job.CompletedAtUtc ?? timeProvider.GetUtcNow().UtcDateTime,
            },
            JsonSerializerOptions.Web
        );

        OutgoingMessages messages = new();
        // The job id doubles as the delivery's idempotency key (X-Webhook-Event-Id) so retried
        // deliveries are de-dupable by the receiver.
        messages.Add(
            new SendWebhookCallbackCommand(job.CallbackUrl, serializedPayload, job.Id.ToString())
        );
        return messages;
    }
}
