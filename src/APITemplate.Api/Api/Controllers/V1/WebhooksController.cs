using APITemplate.Api.Controllers;
using APITemplate.Api.Filters.Webhooks;
using APITemplate.Application.Common.BackgroundJobs;
using APITemplate.Application.Features.Examples.DTOs;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace APITemplate.Api.Controllers.V1;

[ApiVersion(1.0)]
/// <summary>
/// Presentation-layer controller that receives inbound webhook payloads, validates the
/// HMAC signature via <see cref="ValidateWebhookSignatureAttribute"/>, and enqueues them for
/// asynchronous processing (max 1 MB).
/// </summary>
public sealed class WebhooksController : ApiControllerBase
{
    private readonly IWebhookProcessingQueue _queue;

    public WebhooksController(IWebhookProcessingQueue queue) => _queue = queue;

    /// <summary>
    /// Validates the HMAC signature on the incoming payload and enqueues it for background
    /// processing, returning 200 immediately to the sender.
    /// </summary>
    [HttpPost]
    [AllowAnonymous]
    [ValidateWebhookSignature]
    [RequestSizeLimit(1024 * 1024)] // 1 MB max for webhook payloads
    public async Task<IActionResult> Receive(
        [FromBody] WebhookPayload payload,
        CancellationToken ct
    )
    {
        await _queue.EnqueueAsync(payload, ct);
        return Ok();
    }
}
