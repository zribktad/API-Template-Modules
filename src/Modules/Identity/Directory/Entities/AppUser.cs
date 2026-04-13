using Identity.ValueObjects;

namespace Identity.Directory.Entities;

/// <summary>
///     Domain entity representing an application user belonging to a tenant.
///     Tracks identity information, Keycloak linkage, role, and soft-delete state.
/// </summary>
public sealed class AppUser : IAuditableTenantEntity, IHasId
{
    /// <summary>
    ///     Original username exactly as entered by the user (preserves casing and formatting).
    ///     Setting this property also updates <see cref="NormalizedUsername" />.
    /// </summary>
    public required string Username
    {
        get => field;
        set
        {
            field = string.IsNullOrWhiteSpace(value)
                ? throw new ArgumentException("Username cannot be empty.", nameof(Username))
                : value.Trim();
            NormalizedUsername = NormalizeUsername(field);
        }
    }

    /// <summary>
    ///     Uppercase, trimmed version of the username.
    ///     Used for fast database indexing, case-insensitive uniqueness checks, and reliable logins.
    /// </summary>
    public string NormalizedUsername { get; set; } = string.Empty;

    /// <summary>
    ///     Original email exactly as entered by the user. Required for correct email delivery (RFC compliance).
    ///     Setting this property also updates <see cref="NormalizedEmail" />.
    /// </summary>
    public required Email Email
    {
        get => field;
        set
        {
            field = value;
            NormalizedEmail = value.Normalize();
        }
    }

    /// <summary>
    ///     Uppercase, trimmed version of the email.
    ///     Used for fast database indexing, case-insensitive uniqueness checks, and reliable logins.
    /// </summary>
    public string NormalizedEmail { get; set; } = string.Empty;

    /// <summary>
    ///     The user's subject ID in Keycloak. Nullable — existing users may not have one yet.
    /// </summary>
    public string? KeycloakUserId { get; set; }

    public bool IsActive { get; set; } = true;
    public ProvisioningStatus ProvisioningStatus { get; set; } = ProvisioningStatus.Pending;

    public ICollection<CustomRole> Roles { get; set; } = new List<CustomRole>();

    public Guid TenantId { get; set; }
    public AuditInfo Audit { get; set; } = new();
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAtUtc { get; set; }
    public Guid? DeletedBy { get; set; }
    public Guid Id { get; set; }

    /// <summary>
    ///     Links this user to their Keycloak account. Can only be called once — throws if already linked.
    /// </summary>
    public void LinkKeycloak(string keycloakUserId)
    {
        if (KeycloakUserId is not null)
            throw new InvalidOperationException(
                $"AppUser {Id} is already linked to Keycloak account '{KeycloakUserId}'."
            );
        KeycloakUserId = keycloakUserId;
        ProvisioningStatus = ProvisioningStatus.Completed;
    }

    public static AppUser Create(
        string username,
        Email email,
        string? keycloakUserId,
        Guid? tenantId = null,
        bool isActive = true
    )
    {
        AppUser user = new()
        {
            Id = Guid.NewGuid(),
            Username = username,
            Email = email,
            KeycloakUserId = keycloakUserId,
            TenantId = tenantId ?? Guid.Empty,
            IsActive = isActive,
        };

        if (keycloakUserId is not null)
            user.ProvisioningStatus = ProvisioningStatus.Completed;

        return user;
    }

    /// <summary>Returns the canonical form of a username: trimmed and converted to uppercase invariant.</summary>
    public static string NormalizeUsername(string username)
    {
        return username.Trim().ToUpperInvariant();
    }
}
