using APITemplate.Application.Common.Errors;
using APITemplate.Application.Features.Examples.DTOs;
using APITemplate.Domain.Interfaces;
using ErrorOr;

namespace APITemplate.Application.Features.Examples;

public sealed record GetJobStatusQuery(GetJobStatusRequest Request);

public sealed class GetJobStatusQueryHandler
{
    public static async Task<ErrorOr<JobStatusResponse>> HandleAsync(
        GetJobStatusQuery query,
        IJobExecutionRepository repository,
        CancellationToken ct
    )
    {
        var entity = await repository.GetByIdAsync(query.Request.Id, ct);
        return entity is null
            ? DomainErrors.General.NotFound("JobExecution", query.Request.Id)
            : JobResponseMapper.MapToResponse(entity);
    }
}
