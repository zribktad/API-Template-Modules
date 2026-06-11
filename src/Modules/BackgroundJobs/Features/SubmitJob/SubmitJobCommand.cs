using Wolverine;

namespace BackgroundJobs.Features;

public sealed record SubmitJobCommand(SubmitJobRequest Request);

public sealed class SubmitJobCommandHandler
{
    public static async Task<(ErrorOr<JobStatusResponse>, OutgoingMessages)> HandleAsync(
        SubmitJobCommand command,
        IJobExecutionRepository repository,
        IUnitOfWork<BackgroundJobsDbMarker> unitOfWork,
        TimeProvider timeProvider,
        IIdGenerator idGenerator,
        CancellationToken ct
    )
    {
        JobExecution entity = JobExecution.Create(
            idGenerator.NewId(),
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

        // Drive processing via a durable Wolverine command instead of an in-memory channel.
        // UseDurableLocalQueues persists the cascaded message in PostgreSQL, so it survives a
        // restart and is retried/dead-lettered by Wolverine — no silent at-most-once downgrade.
        OutgoingMessages messages = new();
        messages.Add(new ProcessJobCommand(entity.Id));
        return (entity.ToResponse(), messages);
    }
}
