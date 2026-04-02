using APITemplate.Domain.Enums;

namespace APITemplate.Domain.Entities;

/// <summary>
/// Domain entity representing an application user belonging to a tenant.
/// Tracks identity information, Keycloak linkage, role, and soft-delete state.
/// </summary>
public sealed class AppUser : IAuditableTenantEntity, IHasId
{
    public Guid Id { get; set; }

    /// <summary>
    /// Original username exactly as entered by the user (preserves casing and formatting).
    /// </summary>
    public required string Username { get; set; }

    /// <summary>
    /// Uppercase, trimmed version of the username.
    /// Used for fast database indexing, case-insensitive uniqueness checks (preventing impersonation), and reliable logins.
    /// </summary>
    public string NormalizedUsername { get; set; } = string.Empty;

    /// <summary>
    /// Original email exactly as entered by the user. Required for correct email delivery (RFC compliance).
    /// </summary>
    public required string Email { get; set; }

    /// <summary>
    /// Uppercase, trimmed version of the email.
    /// Used for fast database indexing, case-insensitive uniqueness checks (preventing impersonation), and reliable logins.
    /// </summary>
    public string NormalizedEmail { get; set; } = string.Empty;

    /// <summary>
    /// The user's subject ID in Keycloak. Nullable — existing users may not have one yet.
    /// </summary>
    public string? KeycloakUserId { get; set; }

    public bool IsActive { get; set; } = true;
    public UserRole Role { get; set; } = UserRole.User;

    public Tenant Tenant { get; set; } = null!;

    public Guid TenantId { get; set; }
    public AuditInfo Audit { get; set; } = new();
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAtUtc { get; set; }
    public Guid? DeletedBy { get; set; }

    /// <summary>
    /// Returns the canonical form of a username: trimmed and converted to uppercase invariant.
    /// </summary>
    public static string NormalizeUsername(string username) => username.Trim().ToUpperInvariant();

    /// <summary>
    /// Returns the canonical form of an email address: trimmed and converted to uppercase invariant.
    /// </summary>
    public static string NormalizeEmail(string email) => email.Trim().ToUpperInvariant();
}
