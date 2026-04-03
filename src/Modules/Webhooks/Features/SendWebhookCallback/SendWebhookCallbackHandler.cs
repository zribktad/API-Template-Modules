using SharedKernel.Contracts.Commands.Webhooks;
using Webhooks.Contracts;
using Webhooks.Security;
using Webhooks.Services;

namespace Webhooks.Features.SendWebhookCallback;

/// <summary>
/// Wolverine handler that processes <see cref="SendWebhookCallbackCommand"/> from the BackgroundJobs module
/// by enqueuing the payload into the outgoing webhook queue for delivery.
/// </summary>
public sealed class SendWebhookCallbackHandler
{
    public static async Task HandleAsync(
        SendWebhookCallbackCommand command,
        IOutgoingWebhookQueue outgoingQueue,
        CancellationToken ct
    )
    {
        OutgoingWebhookItem item = new(command.CallbackUrl, command.SerializedPayload);
        await outgoingQueue.EnqueueAsync(item, ct);
    }
}
