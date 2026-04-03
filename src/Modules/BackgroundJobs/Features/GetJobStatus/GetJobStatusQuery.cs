namespace BackgroundJobs.Features;

public sealed record GetJobStatusQuery(GetJobStatusRequest Request);

public sealed class GetJobStatusQueryHandler
{
    public static async Task<ErrorOr<JobStatusResponse>> HandleAsync(
        GetJobStatusQuery query,
        IJobExecutionRepository repository,
        CancellationToken ct
    )
    {
        JobExecution? entity = await repository.GetByIdAsync(query.Request.Id, ct);
        return entity is null
            ? DomainErrors.General.NotFound("JobExecution", query.Request.Id)
            : entity.ToResponse();
    }
}



