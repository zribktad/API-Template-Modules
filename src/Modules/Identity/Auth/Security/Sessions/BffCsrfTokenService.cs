using System.Security.Cryptography;
using System.Text;
using Identity.Auth.Security;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.WebUtilities;

namespace Identity.Auth.Security.Sessions;

/// <inheritdoc cref="IBffCsrfTokenService" />
public sealed class BffCsrfTokenService : IBffCsrfTokenService
{
    private const string ProtectorPurpose = "Identity.Bff.CsrfToken.v1";
    private readonly IDataProtector _protector;

    public BffCsrfTokenService(IDataProtectionProvider dataProtectionProvider)
    {
        _protector = dataProtectionProvider.CreateProtector(ProtectorPurpose);
    }

    /// <inheritdoc />
    public string CreateToken(string sessionId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);
        byte[] payload = Encoding.UTF8.GetBytes(sessionId);
        byte[] protectedBytes = _protector.Protect(payload);
        return WebEncoders.Base64UrlEncode(protectedBytes);
    }

    /// <inheritdoc />
    public bool IsValid(string? sessionId, string? headerValue)
    {
        if (string.IsNullOrEmpty(headerValue))
            return false;

        if (string.Equals(headerValue, AuthConstants.Csrf.HeaderValue, StringComparison.Ordinal))
            return true;

        if (string.IsNullOrWhiteSpace(sessionId))
            return false;

        byte[] decoded;
        try
        {
            decoded = WebEncoders.Base64UrlDecode(headerValue);
        }
        catch
        {
            return false;
        }

        byte[] unprotected;
        try
        {
            unprotected = _protector.Unprotect(decoded);
        }
        catch
        {
            return false;
        }

        ReadOnlySpan<byte> expected = Encoding.UTF8.GetBytes(sessionId);
        return expected.Length == unprotected.Length
            && CryptographicOperations.FixedTimeEquals(expected, unprotected);
    }
}
