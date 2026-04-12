using System.Security.Claims;

namespace Identity.Auth.Security;

/// <summary>
///     Detects Keycloak client-credentials / service users so interactive-user rules (tenant,
///     invitations) can be skipped consistently across the JWT pipeline and per-request identity.
/// </summary>
public static class KeycloakServiceAccountClaims
{
    /// <summary>
    ///     Keycloak encodes service users with a <c>preferred_username</c> prefix; client-credentials
    ///     flows may omit it while still using <c>service-account-*</c> on <c>sub</c> when <c>azp</c> is set.
    /// </summary>
    public static bool IsServiceAccount(ClaimsPrincipal? principal)
    {
        if (principal is null)
            return false;

        string? username = principal.FindFirstValue(AuthConstants.Claims.PreferredUsername);
        if (
            username != null
            && username.StartsWith(
                AuthConstants.Claims.ServiceAccountUsernamePrefix,
                StringComparison.OrdinalIgnoreCase
            )
        )
            return true;

        if (string.IsNullOrEmpty(principal.FindFirstValue(AuthConstants.Claims.AuthorizedParty)))
            return false;

        string? subject =
            principal.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? principal.FindFirstValue(AuthConstants.Claims.Subject);

        return subject != null
            && subject.StartsWith(
                AuthConstants.Claims.ServiceAccountUsernamePrefix,
                StringComparison.OrdinalIgnoreCase
            );
    }
}
