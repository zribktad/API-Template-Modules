using System.Security.Cryptography;
using APITemplate.Application.Common.Email;

namespace APITemplate.Infrastructure.Security;

/// <summary>
/// Generates cryptographically random tokens and produces their SHA-256 hex digest
/// for safe storage in the database.
/// </summary>
public sealed class SecureTokenGenerator : ISecureTokenGenerator
{
    /// <summary>Generates a 32-byte cryptographically random token encoded as Base64.</summary>
    public string GenerateToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        return Convert.ToBase64String(bytes);
    }

    /// <summary>Computes and returns the lowercase hex-encoded SHA-256 hash of <paramref name="token"/>.</summary>
    public string HashToken(string token)
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes(token);
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexStringLower(hash);
    }
}
