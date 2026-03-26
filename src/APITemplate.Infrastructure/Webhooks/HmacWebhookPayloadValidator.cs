using System.Security.Cryptography;
using System.Text;
using APITemplate.Application.Common.Contracts;
using APITemplate.Application.Common.Options;
using Microsoft.Extensions.Options;

namespace APITemplate.Infrastructure.Webhooks;

/// <summary>
/// Validates incoming webhook payloads by recomputing the HMAC-SHA256 signature and
/// verifying it against the received signature using constant-time comparison, with a
/// configurable timestamp tolerance window to prevent replay attacks.
/// </summary>
public sealed class HmacWebhookPayloadValidator : IWebhookPayloadValidator
{
    private readonly byte[] _keyBytes;
    private readonly int _toleranceSeconds;
    private readonly TimeProvider _timeProvider;

    public HmacWebhookPayloadValidator(IOptions<WebhookOptions> options, TimeProvider timeProvider)
    {
        _keyBytes = Encoding.UTF8.GetBytes(options.Value.Secret);
        _toleranceSeconds = options.Value.TimestampToleranceSeconds;
        _timeProvider = timeProvider;
    }

    /// <summary>
    /// Returns <c>true</c> when the <paramref name="timestamp"/> is within the configured tolerance,
    /// the <paramref name="signature"/> is valid hex, and the recomputed HMAC matches via constant-time comparison.
    /// </summary>
    public bool IsValid(string payload, string signature, string timestamp)
    {
        if (!long.TryParse(timestamp, out var unixSeconds))
            return false;

        var now = _timeProvider.GetUtcNow().ToUnixTimeSeconds();
        var delta = now > unixSeconds ? now - unixSeconds : unixSeconds - now;
        if (delta > _toleranceSeconds)
            return false;

        var hashBytes = HmacHelper.ComputeHash(_keyBytes, timestamp, payload);

        byte[] signatureBytes;
        try
        {
            signatureBytes = Convert.FromHexString(signature);
        }
        catch (FormatException)
        {
            return false;
        }

        return CryptographicOperations.FixedTimeEquals(hashBytes, signatureBytes);
    }
}
