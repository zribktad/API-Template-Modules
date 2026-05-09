using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using BuildingBlocks.Application.Options;

namespace Identity.Auth.Options;

/// <summary>
///     Configuration for the Backend-for-Frontend (BFF) session layer, including cookie settings,
///     requested OIDC scopes, and token refresh thresholds.
/// </summary>
public sealed class BffOptions : IModuleOptions
{
    public static string SectionName => "Bff";

    [Description("Cookie name used to persist the BFF authenticated session.")]
    [Required]
    [MinLength(1)]
    public string CookieName { get; init; } = ".APITemplate.Auth";

    [Description("Relative or absolute URI used after the user signs out of the BFF session.")]
    [Required]
    [MinLength(1)]
    public string PostLogoutRedirectUri { get; init; } = "/";

    [Description(
        "Optional relative or absolute URI to redirect browser clients when application access is denied "
            + "(e.g. missing invitation). When empty, PostLogoutRedirectUri is used with OAuth2-style query parameters."
    )]
    public string? AccessDeniedRedirectUri { get; init; }

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
            + "Must be >= RefreshLockTimeoutMilliseconds (followers must outlast the lock). "
            + "Should match or exceed the upstream identity provider HTTP timeout "
            + "(e.g. Keycloak's default 10 s) so followers do not give up while the leader is still refreshing."
    )]
    [Range(100, int.MaxValue)]
    public int RefreshWaitTimeoutMilliseconds { get; init; } = 10_000;

    [Description(
        "Distributed refresh lock TTL, in milliseconds. Guards against leader crashes leaving the lock held. "
            + "Must be < RefreshWaitTimeoutMilliseconds (lock must expire before followers give up). "
            + "Should be >= upstream provider HTTP timeout so the leader has enough time to complete the refresh."
    )]
    [Range(100, int.MaxValue)]
    public int RefreshLockTimeoutMilliseconds { get; init; } = 9_000;

    [Description(
        "Refresh coordinator result TTL, in milliseconds. How long the leader's outcome stays in Redis "
            + "for late followers to read. Must be >= RefreshWaitTimeoutMilliseconds so the result is still "
            + "available when the slowest follower finishes waiting."
    )]
    [Range(100, int.MaxValue)]
    public int RefreshResultTtlMilliseconds { get; init; } = 15_000;

    [Description(
        "How long, in hours, terminal sessions (Revoked/Expired) are retained in the database "
            + "for audit trail purposes before the cleanup handler permanently deletes them."
    )]
    [Range(1, int.MaxValue)]
    public int TerminalSessionRetentionHours { get; init; } = 24;

    [Description("When true, a failed refresh revokes only the current BFF session.")]
    public bool RevokeSessionOnRefreshFailure { get; init; } = true;

    [Description("Redis cache TTL, in minutes, for the read-through session cache layer.")]
    [Range(1, int.MaxValue)]
    public int CacheTtlMinutes { get; init; } = 10;

    [Description(
        "Local in-process session cache TTL in seconds. Set to 0 to disable the local cache. "
            + "Bounds staleness window when Redis pub/sub revocation notifications are unavailable."
    )]
    [Range(0, 3600)]
    public int LocalCacheTtlSeconds { get; init; } = 10;

    [Description("Maximum number of session entries retained in the local in-process cache.")]
    [Range(100, int.MaxValue)]
    public int LocalCacheMaxEntries { get; init; } = 10_000;
}
