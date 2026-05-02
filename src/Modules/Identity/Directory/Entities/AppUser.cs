using System.ComponentModel.DataAnnotations.Schema;
using SharedKernel.Infrastructure.Logging;

namespace Identity.Directory.Entities;

/// <summary>
///     Tenant-scoped user. Email and Username are stored as flat primitives so EF Core can build
///     Npgsql-specific indexes directly in C#. <see cref="Email"/> and <see cref="Username"/> are
///     <see cref="NotMappedAttribute"/> facades that expose the domain value object to business logic.
/// </summary>
public sealed class AppUser : IAuditableTenantEntity, IHasId
{
    public const int UsernameMaxLength = 100;
    public const int EmailMaxLength = 320;

    internal string DbEmail { get; private set; } = null!;
    internal string DbNormalizedEmail { get; private set; } = null!;
    internal string DbUsername { get; private set; } = null!;
    internal string DbNormalizedUsername { get; private set; } = null!;

    [NotMapped]
    [PersonalData]
    public NormalizedString Email
    {
        get => new NormalizedString(DbEmail);
        set
        {
            DbEmail = value.Value;
            DbNormalizedEmail = value.Normalized;
        }
    }

    [NotMapped]
    public NormalizedString Username
    {
        get => new NormalizedString(DbUsername);
        set
        {
            DbUsername = value.Value;
            DbNormalizedUsername = value.Normalized;
        }
    }

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
        string email,
        string? keycloakUserId,
        Guid? tenantId = null,
        bool isActive = true
    )
    {
        AppUser user = new()
        {
            Id = Guid.NewGuid(),
            Username = new NormalizedString(username),
            Email = new NormalizedString(email),
            KeycloakUserId = keycloakUserId,
            TenantId = tenantId ?? Guid.Empty,
            IsActive = isActive,
            ProvisioningStatus = keycloakUserId is not null
                ? ProvisioningStatus.Completed
                : ProvisioningStatus.Pending,
        };

        return user;
    }
}
