namespace APITemplate.Application.Features.Examples.DTOs;

/// <summary>
/// Centralises header names and HTTP client identifiers used by the outgoing webhook infrastructure.
/// </summary>
public static class WebhookConstants
{
    public const string SignatureHeader = "X-Webhook-Signature";
    public const string TimestampHeader = "X-Webhook-Timestamp";
    public const string OutgoingHttpClientName = "OutgoingWebhook";
}
