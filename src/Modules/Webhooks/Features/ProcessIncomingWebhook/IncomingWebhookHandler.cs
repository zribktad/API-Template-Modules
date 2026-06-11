using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Webhooks.Contracts;
using Webhooks.Entities;
using Webhooks.Logging;
using Webhooks.Persistence;

namespace Webhooks.Features.ProcessIncomingWebhook;

/// <summary>
///     Wolverine handler that processes an inbound <see cref="WebhookPayload" /> published by
///     <c>WebhooksController</c>. The payload is persisted in Wolverine's durable inbox before this handler
///     runs, so a crash before processing no longer loses the event (the old in-memory channel did). The
///     <see cref="WebhookPayload.EventId" /> is recorded for at-most-once dedup of replays within the
///     signature timestamp tolerance.
/// </summary>
public sealed class IncomingWebhookHandler
{
    public static async Task HandleAsync(
        WebhookPayload payload,
        WebhooksDbContext db,
        IEnumerable<IWebhookEventHandler> handlers,
        TimeProvider timeProvider,
        ILogger<IncomingWebhookHandler> logger,
        CancellationToken ct
    )
    {
        // Deduplicate by EventId (PK) so a replayed request is processed at most once.
        try
        {
            db.IncomingWebhooks.Add(
                new IncomingWebhook
                {
                    EventId = payload.EventId,
                    ProcessedAtUtc = timeProvider.GetUtcNow(),
                }
            );
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            if (await db.IncomingWebhooks.AnyAsync(x => x.EventId == payload.EventId, ct))
            {
                logger.WebhookDuplicateIgnored(payload.EventId);
                return;
            }

            throw;
        }

        bool handled = false;
        foreach (IWebhookEventHandler handler in handlers)
        {
            if (
                handler.EventType == WebhookConstants.WildcardEventType
                || handler.EventType == payload.EventType
            )
            {
                await handler.HandleAsync(payload, ct);
                handled = true;
            }
        }

        if (!handled)
            logger.WebhookNoHandlerRegistered(payload.EventType, payload.EventId);
    }
}
