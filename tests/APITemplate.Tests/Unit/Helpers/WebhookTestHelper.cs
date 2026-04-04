using System.Security.Cryptography;
using System.Text;

namespace APITemplate.Tests.Unit.Helpers;

internal static class WebhookTestHelper
{
    internal static string ComputeHmacSignature(string body, string timestamp, string secret)
    {
        string signedContent = $"{timestamp}.{body}";
        byte[] keyBytes = Encoding.UTF8.GetBytes(secret);
        byte[] contentBytes = Encoding.UTF8.GetBytes(signedContent);
        byte[] hashBytes = HMACSHA256.HashData(keyBytes, contentBytes);
        return Convert.ToHexStringLower(hashBytes);
    }
}
