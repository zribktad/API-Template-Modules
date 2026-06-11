using Asp.Versioning;
using BuildingBlocks.Web.Api;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Webhooks.Contracts;
using Webhooks.Security;
using Wolverine;

namespace Webhooks.Features;

[ApiVersion(1.0)]
public sealed class WebhooksController : ApiControllerBase
{
    private readonly IMessageBus _bus;

    public WebhooksController(IMessageBus bus)
    {
        _bus = bus;
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
        // Publish to Wolverine's durable inbox/local queue. The envelope is persisted in PostgreSQL
        // before we return 2xx, so a crash before IncomingWebhookHandler runs no longer loses the
        // event (unlike the previous in-memory channel).
        await _bus.PublishAsync(payload);
        return Ok();
    }
}
