using Identity.Auth.Security.Sessions;

namespace Identity.Auth.Entities;

/// <summary>
///     PostgreSQL-backed persistent BFF session record. Maps 1:1 to <see cref="BffSessionRecord" />
///     fields, serving as the primary durable store for BFF sessions with Redis as a read-through cache.
/// </summary>
public sealed class BffPersistedSession : IAuditableTenantEntity, IHasId
{
    /// <summary>Opaque identifier used as the cookie session key and store lookup key.</summary>
    public required string SessionId { get; set; }

    /// <summary>Application user identifier associated with the session.</summary>
    public required string UserId { get; set; }

    /// <summary>Stable upstream subject identifier from the identity provider.</summary>
    public required string Subject { get; set; }

    /// <summary>Identity provider backing the session.</summary>
    public BffProviderType Provider { get; set; } = BffProviderType.Keycloak;

    /// <summary>Authorization roles restored into the cookie principal.</summary>
    public string[] Roles { get; set; } = [];

    /// <summary>Email address associated with the signed-in principal, when known.</summary>
    public string? Email { get; set; }

    /// <summary>Display-friendly user name to place into the cookie principal.</summary>
    public string? DisplayName { get; set; }

    /// <summary>Access token payload (encrypted at rest via IDataProtector).</summary>
    public required string EncryptedAccessToken { get; set; }

    /// <summary>Refresh token payload (encrypted at rest via IDataProtector).</summary>
    public required string EncryptedRefreshToken { get; set; }

    /// <summary>ID token payload (encrypted at rest via IDataProtector), when available.</summary>
    public string? EncryptedIdToken { get; set; }

    /// <summary>UTC instant when the current access token expires.</summary>
    public DateTimeOffset AccessTokenExpiresAtUtc { get; set; }

    /// <summary>UTC instant when the refresh token expires, when known.</summary>
    public DateTimeOffset? RefreshTokenExpiresAtUtc { get; set; }

    /// <summary>UTC instant when the session was first created.</summary>
    public DateTimeOffset CreatedAtUtc { get; set; }

    /// <summary>UTC instant when the session was last observed on an incoming request.</summary>
    public DateTimeOffset LastSeenAtUtc { get; set; }

    /// <summary>UTC instant when tokens were last refreshed successfully.</summary>
    public DateTimeOffset LastRefreshedAtUtc { get; set; }

    /// <summary>Lifecycle status of the session record.</summary>
    public BffSessionStatus Status { get; set; } = BffSessionStatus.Active;

    /// <summary>UTC instant when the session was revoked, when applicable.</summary>
    public DateTimeOffset? RevokedAtUtc { get; set; }

    /// <summary>Optimistic concurrency version incremented on every mutation by BffSessionService.</summary>
    public long Version { get; set; }

    /// <summary>Reason the session was revoked or treated as unusable, when applicable.</summary>
    public BffSessionRevocationReason? RevocationReason { get; set; }

    // ── IAuditableTenantEntity ──────────────────────────────────────────────

    public Guid TenantId { get; set; }
    public AuditInfo Audit { get; set; } = new();
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAtUtc { get; set; }
    public Guid? DeletedBy { get; set; }
    public Guid Id { get; set; }
}
