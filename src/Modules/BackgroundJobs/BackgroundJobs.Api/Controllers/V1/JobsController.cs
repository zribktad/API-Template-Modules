using Asp.Versioning;
using BackgroundJobs.Application.Features.Jobs;
using BackgroundJobs.Application.Features.Jobs.DTOs;
using Contracts.Api;
using ErrorOr;
using Microsoft.AspNetCore.Mvc;
using Wolverine;

namespace BackgroundJobs.Api.Controllers.V1;

[ApiVersion(1.0)]
public sealed class JobsController(IMessageBus bus) : ApiControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Submit(SubmitJobRequest request, CancellationToken ct)
    {
        ErrorOr<JobStatusResponse> result = await bus.InvokeAsync<ErrorOr<JobStatusResponse>>(
            new SubmitJobCommand(request), ct);

        if (result.IsError)
            return result.ToErrorResult(this);

        return AcceptedAtAction(
            nameof(GetStatus),
            new { id = result.Value.Id, version = this.GetApiVersion() },
            result.Value
        );
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<JobStatusResponse>> GetStatus(
        [FromRoute] GetJobStatusRequest request,
        CancellationToken ct
    )
    {
        ErrorOr<JobStatusResponse> result = await bus.InvokeAsync<ErrorOr<JobStatusResponse>>(
            new GetJobStatusQuery(request), ct);

        return result.ToActionResult(this);
    }
}
