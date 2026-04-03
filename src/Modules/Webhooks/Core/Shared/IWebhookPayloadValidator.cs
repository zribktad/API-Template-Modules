namespace Webhooks.Core.Shared;

/// <summary>
/// Application-layer abstraction for verifying the authenticity of inbound webhook payloads.
/// </summary>
public interface IWebhookPayloadValidator
{
    bool IsValid(string payload, string signature, string timestamp);
}
