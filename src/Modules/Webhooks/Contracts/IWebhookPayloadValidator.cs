namespace Webhooks.Contracts;

/// <summary>
///     Application-layer abstraction for verifying the authenticity of inbound webhook payloads.
/// </summary>
public interface IWebhookPayloadValidator
{
    public bool IsValid(string payload, string signature, string timestamp);
}
