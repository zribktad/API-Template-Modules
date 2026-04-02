using System.Security.Cryptography;
using System.Text;

namespace APITemplate.Infrastructure.Webhooks;

/// <summary>
/// Internal helper that computes the HMAC-SHA256 signature over a timestamp-prefixed payload,
/// shared by both the signer and validator to ensure consistent signing format.
/// </summary>
internal static class HmacHelper
{
    /// <summary>
    /// Computes HMAC-SHA256 over the string <c>{timestamp}.{payload}</c> using the given key bytes.
    /// </summary>
    public static byte[] ComputeHash(byte[] keyBytes, string timestamp, string payload)
    {
        var signedContent = $"{timestamp}.{payload}";
        var contentBytes = Encoding.UTF8.GetBytes(signedContent);
        return HMACSHA256.HashData(keyBytes, contentBytes);
    }
}
