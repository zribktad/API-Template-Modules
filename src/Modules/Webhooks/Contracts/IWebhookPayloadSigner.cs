namespace Webhooks.Contracts;

/// <summary>
/// Application-layer abstraction for signing outgoing webhook payloads so that receivers can
/// verify authenticity.
/// </summary>
public interface IWebhookPayloadSigner
{
    WebhookSignatureResult Sign(string payload);
}

/// <summary>
/// Value object containing the computed HMAC signature and the timestamp used as the signing input.
/// </summary>
public sealed record WebhookSignatureResult(string Signature, string Timestamp);



