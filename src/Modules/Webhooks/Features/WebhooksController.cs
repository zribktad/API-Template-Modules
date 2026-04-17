using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SharedKernel.Contracts.Api;
using Webhooks.Contracts;
using Webhooks.Security;

namespace Webhooks.Features;

[ApiVersion(1.0)]
public sealed class WebhooksController : ApiControllerBase
{
    private readonly IWebhookProcessingQueue _queue;

    public WebhooksController(IWebhookProcessingQueue queue)
    {
        _queue = queue;
    }

    [HttpPost]
    [AllowAnonymous]
    [ValidateWebhookSignature]
    // Primary size guard: Kestrel rejects oversized bodies at the server level before they
    // are buffered — cheaper and earlier than the application-layer check in
    // WebhookSignatureResourceFilter (which keeps an identical bufferLimit as a safety net).
    // Keep this value in sync with WebhookSignatureResourceFilter.MaxBodyBytes.
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
