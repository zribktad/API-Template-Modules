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

    [Description("Maximum idle session lifetime, in minutes, for the BFF authentication cookie.")]
    [Range(1, int.MaxValue)]
    public int SessionTimeoutMinutes { get; init; } = 60;

    [Description(
        "Maximum idle session lifetime, in minutes, for the server-side BFF session record."
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

    [Description("Maximum time, in milliseconds, follower requests wait for an in-flight refresh.")]
    [Range(100, int.MaxValue)]
    public int RefreshWaitTimeoutMilliseconds { get; init; } = 2000;

    [Description("Distributed refresh lock TTL, in milliseconds.")]
    [Range(100, int.MaxValue)]
    public int RefreshLockTimeoutMilliseconds { get; init; } = 5000;

    [Description("Refresh coordinator result TTL, in milliseconds.")]
    [Range(100, int.MaxValue)]
    public int RefreshResultTtlMilliseconds { get; init; } = 5000;

    [Description("When true, a failed refresh revokes only the current BFF session.")]
    public bool RevokeSessionOnRefreshFailure { get; init; } = true;

    /// <summary>
    ///     Returns the effective idle timeout used by the server-side session store.
    /// </summary>
    public int GetEffectiveSessionIdleTimeoutMinutes() =>
        SessionIdleTimeoutMinutes > 0 ? SessionIdleTimeoutMinutes : SessionTimeoutMinutes;

    /// <summary>
    ///     Returns the effective absolute lifetime cap for a BFF session.
    /// </summary>
    public int GetEffectiveSessionAbsoluteLifetimeMinutes() =>
        SessionAbsoluteLifetimeMinutes > 0
            ? SessionAbsoluteLifetimeMinutes
            : GetEffectiveSessionIdleTimeoutMinutes();
}
