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
        catch (Exception ex)
        {
            entity.MarkFailed($"Failed to enqueue job for processing: {ex.Message}", timeProvider);

            await unitOfWork.ExecuteInTransactionAsync(
                async () =>
                {
                    await repository.UpdateAsync(entity, ct);
                },
                ct
            );

            return Error.Failure(
                ErrorCatalog.General.Unknown,
                "Failed to enqueue job for processing."
            );
        }

        return entity.ToResponse();
    }
}
