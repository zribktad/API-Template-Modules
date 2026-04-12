namespace Identity.Auth.Security.Sessions;

/// <summary>
///     Canonical server-side representation of a BFF session, including identity data, token
///     material, lifecycle timestamps, and optimistic concurrency metadata.
/// </summary>
public sealed record BffSessionRecord
{
    /// <summary>Opaque identifier used as the cookie session key and store lookup key.</summary>
    public string SessionId { get; init; } = string.Empty;

    /// <summary>Application user identifier associated with the session.</summary>
    public string UserId { get; init; } = string.Empty;

    /// <summary>Stable upstream subject identifier from the identity provider.</summary>
    public string Subject { get; init; } = string.Empty;

    /// <summary>Identity provider backing the session.</summary>
    public BffProviderType Provider { get; init; } = BffProviderType.Keycloak;

    /// <summary>Tenant identifier projected into the session when available.</summary>
    public string? TenantId { get; init; }

    /// <summary>Authorization roles restored into the cookie principal.</summary>
    public string[] Roles { get; init; } = [];

    /// <summary>Email address associated with the signed-in principal, when known.</summary>
    public string? Email { get; init; }

    /// <summary>Display-friendly user name to place into the cookie principal.</summary>
    public string? DisplayName { get; init; }

    /// <summary>Access token payload used by downstream BFF calls (plaintext in memory, encrypted at rest).</summary>
    public string AccessToken { get; init; } = string.Empty;

    /// <summary>Refresh token payload used for silent token renewal (plaintext in memory, encrypted at rest).</summary>
    public string RefreshToken { get; init; } = string.Empty;

    /// <summary>ID token payload, when the provider returned one (plaintext in memory, encrypted at rest).</summary>
    public string? IdToken { get; init; }

    /// <summary>UTC instant when the current access token expires.</summary>
    public DateTimeOffset AccessTokenExpiresAtUtc { get; init; }

    /// <summary>UTC instant when the refresh token expires, when known.</summary>
    public DateTimeOffset? RefreshTokenExpiresAtUtc { get; init; }

    /// <summary>UTC instant when the session was first created.</summary>
    public DateTimeOffset CreatedAtUtc { get; init; }

    /// <summary>UTC instant when the session was last observed on an incoming request.</summary>
    public DateTimeOffset LastSeenAtUtc { get; init; }

    /// <summary>UTC instant when tokens were last refreshed successfully.</summary>
    public DateTimeOffset LastRefreshedAtUtc { get; init; }

    /// <summary>Lifecycle status of the session record.</summary>
    public BffSessionStatus Status { get; init; } = BffSessionStatus.Active;

    /// <summary>Optimistic concurrency version incremented on every mutation.</summary>
    public long Version { get; init; }

    /// <summary>UTC instant when the session was revoked, when applicable.</summary>
    public DateTimeOffset? RevokedAtUtc { get; init; }

    /// <summary>Reason the session was revoked or considered invalid, when applicable.</summary>
    public BffSessionRevocationReason? RevocationReason { get; init; }
}
