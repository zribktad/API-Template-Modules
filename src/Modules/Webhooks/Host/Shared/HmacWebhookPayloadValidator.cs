using System.Security.Cryptography;
using Microsoft.Extensions.Options;
using Webhooks.Core.Shared;

namespace Webhooks.Host.Shared;

public sealed class HmacWebhookPayloadValidator : IWebhookPayloadValidator
{
    private readonly byte[] _keyBytes;
    private readonly int _toleranceSeconds;
    private readonly TimeProvider _timeProvider;

    public HmacWebhookPayloadValidator(IOptions<WebhookOptions> options, TimeProvider timeProvider)
    {
        _keyBytes = HmacHelper.GetKeyBytes(options.Value.Secret);
        _toleranceSeconds = options.Value.TimestampToleranceSeconds;
        _timeProvider = timeProvider;
    }

    public bool IsValid(string payload, string signature, string timestamp)
    {
        if (!long.TryParse(timestamp, out long unixSeconds))
            return false;

        long now = _timeProvider.GetUtcNow().ToUnixTimeSeconds();
        long delta = now > unixSeconds ? now - unixSeconds : unixSeconds - now;
        if (delta > _toleranceSeconds)
            return false;

        byte[] hashBytes = HmacHelper.ComputeHash(_keyBytes, timestamp, payload);

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
