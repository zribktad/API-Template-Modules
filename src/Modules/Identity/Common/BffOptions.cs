using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace Identity.Options;

/// <summary>
///     Configuration for the Backend-for-Frontend (BFF) session layer, including cookie settings,
///     requested OIDC scopes, and token refresh thresholds.
/// </summary>
public sealed class BffOptions
{
    [Description("Cookie name used to persist the BFF authenticated session.")]
    [Required]
    [MinLength(1)]
    public string CookieName { get; init; } = ".APITemplate.Auth";

    [Description("Relative or absolute URI used after the user signs out of the BFF session.")]
    [Required]
    [MinLength(1)]
    public string PostLogoutRedirectUri { get; init; } = "/";

    [Description(
        "Maximum idle session lifetime, in minutes, for both the BFF authentication cookie and the server-side session record."
    )]
    [Range(1, int.MaxValue)]
    public int SessionIdleTimeoutMinutes { get; init; } = 60;

    [Description("Maximum absolute session lifetime, in minutes, regardless of activity.")]
    [Range(1, int.MaxValue)]
    public int SessionAbsoluteLifetimeMinutes { get; init; } = 480;

    [Description("OIDC scopes requested during sign-in.")]
    [Required]
    [MinLength(1)]
    public string[] Scopes { get; init; } = [.. AuthConstants.Scopes.Default];

    [Description("Minutes before access token expiry when proactive refresh should begin.")]
    [Range(0, int.MaxValue)]
    public int RefreshThresholdMinutes { get; init; } = 2;

    [Description(
        "Maximum time, in milliseconds, follower requests wait for an in-flight refresh. "
            + "Should be aligned with (or exceed) the upstream identity provider HTTP timeout "
            + "(e.g. Keycloak's default 10 s) so followers do not give up while the leader is still refreshing."
    )]
    [Range(100, int.MaxValue)]
    public int RefreshWaitTimeoutMilliseconds { get; init; } = 10_000;

    [Description("Distributed refresh lock TTL, in milliseconds.")]
    [Range(100, int.MaxValue)]
    public int RefreshLockTimeoutMilliseconds { get; init; } = 5000;

    [Description("Refresh coordinator result TTL, in milliseconds.")]
    [Range(100, int.MaxValue)]
    public int RefreshResultTtlMilliseconds { get; init; } = 5000;

    [Description("When true, a failed refresh revokes only the current BFF session.")]
    public bool RevokeSessionOnRefreshFailure { get; init; } = true;

    [Description("Redis cache TTL, in minutes, for the read-through session cache layer.")]
    [Range(1, int.MaxValue)]
    public int CacheTtlMinutes { get; init; } = 10;
}
