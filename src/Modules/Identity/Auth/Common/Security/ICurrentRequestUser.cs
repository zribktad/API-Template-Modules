namespace Identity.Auth.Security;

/// <summary>
///     Snapshot of the authenticated principal for the current HTTP request (BFF cookie or JWT bearer).
///     Centralizes claim interpretation so controllers do not duplicate <see cref="System.Security.Claims.ClaimsPrincipal" /> parsing.
/// </summary>
/// <remarks>
///     <para>
///         <see cref="ApplicationUserId" /> is set when <c>NameIdentifier</c> is a GUID that is not the same string as
///         the OIDC <c>sub</c> (typical BFF session: local <c>AppUser.Id</c> vs Keycloak subject). When JWT maps
///         <c>sub</c> onto <c>NameIdentifier</c> only, both match and <see cref="ApplicationUserId" /> stays unset —
///         use <see cref="OidcSubject" /> for Keycloak-linked lookups.
///     </para>
/// </remarks>
public interface ICurrentRequestUser
{
    /// <summary>Keycloak / OIDC subject (<c>sub</c>), when present.</summary>
    string? OidcSubject { get; }

    /// <summary>Local application user id when carried distinctly from <see cref="OidcSubject" /> (BFF cookie).</summary>
    Guid? ApplicationUserId { get; }

    /// <summary><c>preferred_username</c> or mapped name claim.</summary>
    string? PreferredUsername { get; }

    /// <summary>False for Keycloak service accounts (<c>service-account-*</c>).</summary>
    bool IsInteractiveUser { get; }
}
