namespace Identity.Security.Sessions;

/// <summary>
///     Enumerates the reasons a BFF session may be revoked or treated as unusable.
/// </summary>
public enum BffSessionRevocationReason
{
    /// <summary>Session ended due to an explicit logout request.</summary>
    Logout = 0,

    /// <summary>The provider explicitly rejected the refresh token grant.</summary>
    RefreshRejected = 1,

    /// <summary>The session no longer contains a refresh token required for renewal.</summary>
    RefreshTokenMissing = 2,

    /// <summary>A refresh flow indicated suspicious refresh token reuse.</summary>
    RefreshTokenReplaySuspected = 3,

    /// <summary>The stored session payload is malformed or cannot be trusted.</summary>
    SessionCorrupted = 4,

    /// <summary>The upstream provider session or token state is no longer valid.</summary>
    ProviderSessionInvalid = 5,

    /// <summary>The session exceeded the configured absolute lifetime cap.</summary>
    AbsoluteLifetimeExceeded = 6,
}
