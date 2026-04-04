using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using SharedKernel.Contracts.Api;
using SharedKernel.Contracts.Security;
using Wolverine;

namespace BackgroundJobs.Features;

[ApiVersion(1.0)]
public sealed class JobsController(IMessageBus bus) : ApiControllerBase
{
    [HttpPost]
    [RequirePermission(Permission.Examples.Execute)]
    public async Task<IActionResult> Submit(SubmitJobRequest request, CancellationToken ct)
    {
        ErrorOr<JobStatusResponse> result = await bus.InvokeAsync<ErrorOr<JobStatusResponse>>(
            new SubmitJobCommand(request),
            ct
        );

        if (result.IsError)
            return result.ToErrorResult(this);

        return AcceptedAtAction(
            nameof(GetStatus),
            new { id = result.Value.Id, version = this.GetApiVersion() },
            result.Value
        );
    }

    [HttpGet("{id:guid}")]
    [RequirePermission(Permission.Examples.Read)]
    public async Task<ActionResult<JobStatusResponse>> GetStatus(
        [FromRoute] GetJobStatusRequest request,
        CancellationToken ct
    )
    {
        ErrorOr<JobStatusResponse> result = await bus.InvokeAsync<ErrorOr<JobStatusResponse>>(
            new GetJobStatusQuery(request),
            ct
        );

        return result.ToActionResult(this);
    }
}
