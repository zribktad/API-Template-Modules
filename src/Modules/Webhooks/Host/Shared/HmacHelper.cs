using System.Security.Cryptography;
using System.Text;

namespace Webhooks.Host.Shared;

/// <summary>
/// Internal helper that computes the HMAC-SHA256 signature over a timestamp-prefixed payload.
/// </summary>
internal static class HmacHelper
{
    public static byte[] GetKeyBytes(string secret) => Encoding.UTF8.GetBytes(secret);

    public static byte[] ComputeHash(byte[] keyBytes, string timestamp, string payload)
    {
        string signedContent = $"{timestamp}.{payload}";
        byte[] contentBytes = Encoding.UTF8.GetBytes(signedContent);
        return HMACSHA256.HashData(keyBytes, contentBytes);
    }
}
