using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SharedKernel.Contracts.Api;
using Webhooks.Api.Filters;
using Webhooks.Application.Contracts;
using Webhooks.Application.DTOs;

namespace Webhooks.Api.Controllers.V1;

[ApiVersion(1.0)]
public sealed class WebhooksController : ApiControllerBase
{
    private readonly IWebhookProcessingQueue _queue;

    public WebhooksController(IWebhookProcessingQueue queue) => _queue = queue;

    [HttpPost]
    [AllowAnonymous]
    [ValidateWebhookSignature]
    [RequestSizeLimit(1024 * 1024)]
    public async Task<IActionResult> Receive(
        [FromBody] WebhookPayload payload,
        CancellationToken ct
    )
    {
        await _queue.EnqueueAsync(payload, ct);
        return Ok();
    }
}
