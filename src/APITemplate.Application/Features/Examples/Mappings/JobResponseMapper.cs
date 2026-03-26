using APITemplate.Application.Features.Examples.DTOs;
using APITemplate.Domain.Entities;

namespace APITemplate.Application.Features.Examples;

internal static class JobResponseMapper
{
    internal static JobStatusResponse MapToResponse(JobExecution entity) =>
        new(
            entity.Id,
            entity.JobType,
            entity.Status,
            entity.ProgressPercent,
            entity.Parameters,
            entity.ResultPayload,
            entity.ErrorMessage,
            entity.SubmittedAtUtc,
            entity.StartedAtUtc,
            entity.CompletedAtUtc,
            entity.CallbackUrl
        );
}
