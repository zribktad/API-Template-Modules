namespace APITemplate.Application.Common.Email;

/// <summary>
/// Application-layer abstraction for generating and hashing cryptographically secure tokens
/// used in email verification flows (e.g. invitation acceptance, password reset).
/// </summary>
public interface ISecureTokenGenerator
{
    /// <summary>Generates a new cryptographically random token suitable for use in email links.</summary>
    string GenerateToken();

    /// <summary>
    /// Returns a one-way hash of <paramref name="token"/> for safe storage in the database,
    /// allowing verification without storing the raw token.
    /// </summary>
    string HashToken(string token);
}
