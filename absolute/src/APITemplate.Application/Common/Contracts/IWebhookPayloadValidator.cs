namespace APITemplate.Application.Common.Contracts;

/// <summary>
/// Application-layer abstraction for verifying the authenticity of inbound webhook payloads
/// by validating their HMAC signature against the shared secret.
/// </summary>
public interface IWebhookPayloadValidator
{
    /// <summary>
    /// Returns <c>true</c> when the computed HMAC of <paramref name="payload"/> and
    /// <paramref name="timestamp"/> matches <paramref name="signature"/>; <c>false</c> otherwise.
    /// </summary>
    bool IsValid(string payload, string signature, string timestamp);
}
