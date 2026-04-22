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
            // The job never entered Processing, so it cannot be transitioned to Failed.
            // Remove the uncommitted record to avoid a dangling Pending entry.
            await unitOfWork.ExecuteInTransactionAsync(
                async () =>
                {
                    await repository.DeleteAsync(entity, ct);
                },
                ct
            );

            return Error.Failure(
                ErrorCatalog.General.Unknown,
                $"Failed to enqueue job for processing: {ex.Message}"
            );
        }

        return entity.ToResponse();
    }
}
