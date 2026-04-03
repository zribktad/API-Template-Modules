namespace Webhooks.Core.Shared;

/// <summary>
/// Centralises header names and HTTP client identifiers used by the webhook infrastructure.
/// </summary>
public static class WebhookConstants
{
    public const string SignatureHeader = "X-Webhook-Signature";
    public const string TimestampHeader = "X-Webhook-Timestamp";
    public const string OutgoingHttpClientName = "OutgoingWebhook";
    public const string WildcardEventType = "*";
}
