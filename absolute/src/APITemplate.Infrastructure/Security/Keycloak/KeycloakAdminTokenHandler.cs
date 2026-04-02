using System.Net.Http.Headers;

namespace APITemplate.Infrastructure.Security.Keycloak;

/// <summary>
/// A transient <see cref="DelegatingHandler"/> that attaches a cached Keycloak
/// service-account Bearer token to every outbound admin API request.
/// Token acquisition and caching are delegated to the singleton <see cref="KeycloakAdminTokenProvider"/>.
/// </summary>
public sealed class KeycloakAdminTokenHandler : DelegatingHandler
{
    private readonly KeycloakAdminTokenProvider _tokenProvider;

    public KeycloakAdminTokenHandler(KeycloakAdminTokenProvider tokenProvider)
    {
        _tokenProvider = tokenProvider;
    }

    /// <summary>
    /// Fetches a valid service-account token from <see cref="KeycloakAdminTokenProvider"/>
    /// and attaches it as a Bearer Authorization header before forwarding the request.
    /// </summary>
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken
    )
    {
        var token = await _tokenProvider.GetTokenAsync(cancellationToken);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return await base.SendAsync(request, cancellationToken);
    }
}
