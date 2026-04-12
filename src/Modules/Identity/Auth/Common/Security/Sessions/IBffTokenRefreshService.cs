namespace Identity.Auth.Security.Sessions;

/// <summary>
///     Performs silent token refresh for BFF sessions when access tokens are close to expiry.
/// </summary>
public interface IBffTokenRefreshService
{
    /// <summary>
    ///     Refreshes session tokens when required and returns the resulting refresh outcome.
    /// </summary>
    Task<BffRefreshOutcome> RefreshIfRequiredAsync(
        BffSessionRecord session,
        CancellationToken ct = default
    );
}
