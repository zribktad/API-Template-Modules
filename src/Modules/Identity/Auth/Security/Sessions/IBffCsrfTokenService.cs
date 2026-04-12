namespace Identity.Auth.Security.Sessions;

/// <summary>
///     Issues and validates anti-CSRF header values bound to a BFF session id using ASP.NET Data
///     Protection (tamper-proof, no extra DB columns).
/// </summary>
public interface IBffCsrfTokenService
{
    string CreateToken(string sessionId);

    /// <summary>
    ///     Returns true when <paramref name="sessionId" /> is non-empty and <paramref name="headerValue" />
    ///     is a Data Protection token whose payload matches the session id.
    /// </summary>
    bool IsValid(string? sessionId, string? headerValue);
}
