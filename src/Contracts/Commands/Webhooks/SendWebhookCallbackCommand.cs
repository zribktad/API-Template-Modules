namespace Contracts.Commands.Webhooks;

/// <summary>
/// Cross-module command instructing the Webhooks module to deliver an outgoing webhook callback.
/// Dispatched by the BackgroundJobs job processor when a job with a callback URL completes.
/// </summary>
public sealed record SendWebhookCallbackCommand(string CallbackUrl, string SerializedPayload);
