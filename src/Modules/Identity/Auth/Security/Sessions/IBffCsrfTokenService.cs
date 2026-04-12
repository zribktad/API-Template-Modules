namespace Identity.Auth.Security.Sessions;

/// <summary>
///     Issues and validates anti-CSRF header values bound to a BFF session id using ASP.NET Data
///     Protection (tamper-proof, no extra DB columns). Legacy plain <c>1</c> remains accepted.
/// </summary>
public interface IBffCsrfTokenService
{
    string CreateToken(string sessionId);

    /// <summary>
    ///     Returns true when <paramref name="headerValue" /> is the legacy constant, or when
    ///     <paramref name="sessionId" /> is set and matches a protected payload.
    /// </summary>
    bool IsValid(string? sessionId, string? headerValue);
}
