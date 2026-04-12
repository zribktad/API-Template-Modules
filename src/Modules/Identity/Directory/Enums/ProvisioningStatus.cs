namespace Identity.Directory.Enums;

/// <summary>
///     Tracks whether a <see cref="Identity.Directory.Entities.AppUser" />'s external identity provider account
///     (Keycloak) has been successfully provisioned.
/// </summary>
public enum ProvisioningStatus
{
    /// <summary>The Keycloak account has not yet been created or linked.</summary>
    Pending = 0,

    /// <summary>The Keycloak account was successfully created and linked to the user.</summary>
    Completed = 1,
}
