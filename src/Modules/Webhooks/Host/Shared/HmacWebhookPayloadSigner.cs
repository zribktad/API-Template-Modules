using Microsoft.Extensions.Options;
using Webhooks.Core.Shared;

namespace Webhooks.Host.Shared;

public sealed class HmacWebhookPayloadSigner : IWebhookPayloadSigner
{
    private readonly byte[] _keyBytes;
    private readonly TimeProvider _timeProvider;

    public HmacWebhookPayloadSigner(IOptions<WebhookOptions> options, TimeProvider timeProvider)
    {
        _keyBytes = HmacHelper.GetKeyBytes(options.Value.Secret);
        _timeProvider = timeProvider;
    }

    public WebhookSignatureResult Sign(string payload)
    {
        string timestamp = _timeProvider.GetUtcNow().ToUnixTimeSeconds().ToString();
        byte[] hashBytes = HmacHelper.ComputeHash(_keyBytes, timestamp, payload);
        string signature = Convert.ToHexStringLower(hashBytes);
        return new WebhookSignatureResult(signature, timestamp);
    }
}
