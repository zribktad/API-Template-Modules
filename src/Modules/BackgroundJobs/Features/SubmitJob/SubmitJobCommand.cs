using BackgroundJobs.Shared;

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
        JobExecution entity = new()
        {
            Id = Guid.NewGuid(),
            JobType = command.Request.JobType,
            Parameters = command.Request.Parameters,
            CallbackUrl = command.Request.CallbackUrl,
            SubmittedAtUtc = timeProvider.GetUtcNow().UtcDateTime,
        };

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
                code: SharedKernel.Application.Errors.ErrorCatalog.General.Unknown,
                description: "Failed to enqueue job for processing."
            );
        }

        return entity.ToResponse();
    }
}



