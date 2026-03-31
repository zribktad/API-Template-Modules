using APITemplate.Application.Common.BackgroundJobs;
using APITemplate.Application.Features.Examples.DTOs;
using APITemplate.Domain.Entities;
using APITemplate.Domain.Interfaces;
using ErrorOr;

namespace APITemplate.Application.Features.Examples;

public sealed record SubmitJobCommand(SubmitJobRequest Request);

public sealed class SubmitJobCommandHandler
{
    public static async Task<ErrorOr<JobStatusResponse>> HandleAsync(
        SubmitJobCommand command,
        IJobExecutionRepository repository,
        IJobQueue jobQueue,
        IUnitOfWork unitOfWork,
        TimeProvider timeProvider,
        CancellationToken ct
    )
    {
        var entity = new JobExecution
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

        await jobQueue.EnqueueAsync(entity.Id, ct);

        return JobResponseMapper.MapToResponse(entity);
    }
}
