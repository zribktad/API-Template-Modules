using Webhooks.Security;

namespace APITemplate.Tests.Unit.Helpers;

internal static class WebhookTestHelper
{
    internal static string ComputeHmacSignature(string body, string timestamp, string secret)
    {
        byte[] hashBytes = HmacHelper.ComputeHash(HmacHelper.GetKeyBytes(secret), timestamp, body);
        return Convert.ToHexStringLower(hashBytes);
    }
}
