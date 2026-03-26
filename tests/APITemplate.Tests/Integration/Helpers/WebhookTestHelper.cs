using System.Security.Cryptography;
using System.Text;

namespace APITemplate.Tests.Integration.Helpers;

internal static class WebhookTestHelper
{
    internal static string ComputeHmacSignature(string body, string timestamp, string secret)
    {
        var signedContent = $"{timestamp}.{body}";
        var keyBytes = Encoding.UTF8.GetBytes(secret);
        var contentBytes = Encoding.UTF8.GetBytes(signedContent);
        var hashBytes = HMACSHA256.HashData(keyBytes, contentBytes);
        return Convert.ToHexStringLower(hashBytes);
    }
}
