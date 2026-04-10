namespace Identity.Security.Sessions;

/// <summary>
///     Identifies the upstream identity provider that issued the tokens backing a BFF session.
/// </summary>
public enum BffProviderType
{
    /// <summary>
    ///     Session tokens were issued by Keycloak.
    /// </summary>
    Keycloak = 0,
}
