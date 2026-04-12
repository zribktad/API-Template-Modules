namespace Identity.Auth.Security.Sessions;

/// <summary>
///     Encrypts and decrypts token material within <see cref="BffSessionRecord" /> instances.
/// </summary>
public interface IBffSessionTokenProtector
{
    /// <summary>
    ///     Returns a copy of <paramref name="session" /> with token fields encrypted.
    /// </summary>
    BffSessionRecord Protect(BffSessionRecord session);

    /// <summary>
    ///     Returns a copy of <paramref name="record" /> with token fields decrypted, or
    ///     <see langword="null" /> when decryption fails (e.g. key rotation).
    /// </summary>
    BffSessionRecord? Unprotect(BffSessionRecord record, string sessionId);
}
