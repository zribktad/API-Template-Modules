using System.Security.Cryptography;
using Identity.Logging;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Logging;

namespace Identity.Security.Sessions;

/// <summary>
///     Encrypts and decrypts token material within <see cref="BffSessionRecord" /> instances using
///     the ASP.NET Core Data Protection stack.
/// </summary>
public sealed class BffSessionTokenProtector : IBffSessionTokenProtector
{
    private readonly IDataProtector _protector;
    private readonly ILogger<BffSessionTokenProtector> _logger;

    public BffSessionTokenProtector(
        IDataProtectionProvider dataProtectionProvider,
        ILogger<BffSessionTokenProtector> logger
    )
    {
        _protector = dataProtectionProvider.CreateProtector("bff:session:tokens");
        _logger = logger;
    }

    /// <inheritdoc />
    public BffSessionRecord Protect(BffSessionRecord session)
    {
        return session with
        {
            AccessToken = _protector.Protect(session.AccessToken),
            RefreshToken = _protector.Protect(session.RefreshToken),
            IdToken = string.IsNullOrWhiteSpace(session.IdToken)
                ? null
                : _protector.Protect(session.IdToken),
        };
    }

    /// <inheritdoc />
    public BffSessionRecord? Unprotect(BffSessionRecord record, string sessionId)
    {
        try
        {
            return record with
            {
                AccessToken = _protector.Unprotect(record.AccessToken),
                RefreshToken = _protector.Unprotect(record.RefreshToken),
                IdToken = record.IdToken is null ? null : _protector.Unprotect(record.IdToken),
            };
        }
        catch (CryptographicException ex)
        {
            _logger.BffSessionUnprotectFailed(ex, sessionId);
            return null;
        }
        catch (FormatException ex)
        {
            _logger.BffSessionPayloadMalformed(ex, sessionId);
            return null;
        }
    }
}
