namespace SharedKernel.Contracts.Commands.Webhooks;

/// <summary>
///     Cross-module command instructing the Webhooks module to deliver an outgoing webhook callback.
///     Dispatched by the BackgroundJobs job processor when a job with a callback URL completes.
///     <paramref name="EventId" /> is sent as the <c>X-Webhook-Event-Id</c> header so receivers can
///     deduplicate retried deliveries.
/// </summary>
public sealed record SendWebhookCallbackCommand(
    string CallbackUrl,
    string SerializedPayload,
    string EventId
);
