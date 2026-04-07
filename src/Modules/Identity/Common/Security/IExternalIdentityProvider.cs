namespace Identity.Security;

/// <summary>
///     Abstraction for external social identity providers available through Keycloak.
///     Implementations describe a specific IdP (e.g. Google, GitHub) and carry the
///     Keycloak <c>kc_idp_hint</c> alias used to trigger a direct redirect.
/// </summary>
public interface IExternalIdentityProvider
{
    /// <summary>Keycloak alias for the identity provider (used as <c>kc_idp_hint</c>).</summary>
    string IdpHint { get; }

    /// <summary>Human-readable name for display in the UI.</summary>
    string DisplayName { get; }
}
