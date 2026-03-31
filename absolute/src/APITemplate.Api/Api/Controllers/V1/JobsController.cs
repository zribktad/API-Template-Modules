using APITemplate.Api.Authorization;
using APITemplate.Api.Controllers;
using APITemplate.Api.ErrorOrMapping;
using APITemplate.Application.Common.Security;
using APITemplate.Application.Features.Examples;
using APITemplate.Application.Features.Examples.DTOs;
using Asp.Versioning;
using ErrorOr;
using Microsoft.AspNetCore.Mvc;
using Wolverine;

namespace APITemplate.Api.Controllers.V1;

[ApiVersion(1.0)]
/// <summary>
/// Presentation-layer controller that demonstrates long-running job submission and
/// asynchronous status polling using a channel-based job queue.
/// </summary>
public sealed class JobsController(IMessageBus bus) : ApiControllerBase
{
    /// <summary>
    /// Enqueues a new job and returns 202 Accepted with a Location header pointing to the
    /// status endpoint so the caller can poll for completion.
    /// </summary>
    [HttpPost]
    [RequirePermission(Permission.Examples.Execute)]
    public async Task<IActionResult> Submit(SubmitJobRequest request, CancellationToken ct)
    {
        var result = await bus.InvokeAsync<ErrorOr<JobStatusResponse>>(
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

    /// <summary>Returns the current execution status of a previously submitted job, or 404 if not found.</summary>
    [HttpGet("{id:guid}")]
    [RequirePermission(Permission.Examples.Read)]
    public async Task<ActionResult<JobStatusResponse>> GetStatus(
        [FromRoute] GetJobStatusRequest request,
        CancellationToken ct
    )
    {
        var result = await bus.InvokeAsync<ErrorOr<JobStatusResponse>>(
            new GetJobStatusQuery(request),
            ct
        );
        return result.ToActionResult(this);
    }
}
