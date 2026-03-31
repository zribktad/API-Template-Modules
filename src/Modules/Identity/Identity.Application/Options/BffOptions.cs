using Identity.Application.Common.Security;

namespace Identity.Application.Options;

/// <summary>
/// Configuration for the Backend-for-Frontend (BFF) session layer, including cookie settings,
/// requested OIDC scopes, and token refresh thresholds.
/// </summary>
public sealed class BffOptions
{
    public string CookieName { get; init; } = ".APITemplate.Auth";
    public string PostLogoutRedirectUri { get; init; } = "/";
    public int SessionTimeoutMinutes { get; init; } = 60;
    public string[] Scopes { get; init; } = [.. AuthConstants.Scopes.Default];
    public int TokenRefreshThresholdMinutes { get; init; } = 2;
}
