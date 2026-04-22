using BackgroundJobs.Logging;
using Microsoft.Extensions.Logging;

namespace BackgroundJobs.Features;

public sealed record SubmitJobCommand(SubmitJobRequest Request);

public sealed class SubmitJobCommandHandler
{
    public static async Task<ErrorOr<JobStatusResponse>> HandleAsync(
        SubmitJobCommand command,
        IJobExecutionRepository repository,
        IJobQueue jobQueue,
        IUnitOfWork<BackgroundJobsDbMarker> unitOfWork,
        TimeProvider timeProvider,
        ILogger<SubmitJobCommandHandler> logger,
        CancellationToken ct
    )
    {
        JobExecution entity = JobExecution.Create(
            command.Request.JobType,
            timeProvider,
            command.Request.Parameters,
            command.Request.CallbackUrl
        );

        await unitOfWork.ExecuteInTransactionAsync(
            async () =>
            {
                await repository.AddAsync(entity, ct);
            },
            ct
        );

        try
        {
            await jobQueue.EnqueueAsync(entity.Id, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.JobEnqueueFailed(ex, entity.Id);

            // The job was persisted as Pending but never entered Processing.
            // Remove the persisted Pending record to avoid leaving a dangling entry.
            await unitOfWork.ExecuteInTransactionAsync(
                async () =>
                {
                    await repository.DeleteAsync(entity, ct);
                },
                ct
            );

            return Error.Failure(
                ErrorCatalog.General.Unknown,
                $"Failed to enqueue job for processing ({ex.GetType().Name})."
            );
        }

        return entity.ToResponse();
    }
}
