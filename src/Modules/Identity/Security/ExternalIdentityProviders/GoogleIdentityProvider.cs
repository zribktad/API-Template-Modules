namespace Identity.Security.ExternalIdentityProviders;

/// <summary>Google social login provider via Keycloak identity brokering.</summary>
internal sealed class GoogleIdentityProvider : IExternalIdentityProvider
{
    public string IdpHint => "google";

    public string DisplayName => "Google";
}
