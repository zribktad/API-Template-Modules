using APITemplate.Application.Common.Options;
using APITemplate.Application.Common.Security;
using APITemplate.Infrastructure.Security;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi;

namespace APITemplate.Api.OpenApi;

/// <summary>
/// OpenAPI document transformer that registers a Keycloak OAuth2 Authorization Code security scheme
/// and adds a global security requirement so Swagger UI can authenticate against the configured realm.
/// </summary>
public sealed class BearerSecuritySchemeDocumentTransformer : IOpenApiDocumentTransformer
{
    private readonly IAuthenticationSchemeProvider _schemeProvider;
    private readonly KeycloakOptions _keycloak;

    public BearerSecuritySchemeDocumentTransformer(
        IAuthenticationSchemeProvider schemeProvider,
        IOptions<KeycloakOptions> keycloakOptions
    )
    {
        _schemeProvider = schemeProvider;
        _keycloak = keycloakOptions.Value;
    }

    /// <summary>
    /// Adds the Keycloak OAuth2 security scheme and a global security requirement to the OpenAPI document,
    /// skipping transformation if the JWT Bearer authentication scheme is not registered.
    /// </summary>
    public async Task TransformAsync(
        OpenApiDocument document,
        OpenApiDocumentTransformerContext context,
        CancellationToken cancellationToken
    )
    {
        var schemes = await _schemeProvider.GetAllSchemesAsync();
        if (!schemes.Any(s => s.Name == JwtBearerDefaults.AuthenticationScheme))
            return;

        var authority = KeycloakUrlHelper.BuildAuthority(_keycloak.AuthServerUrl, _keycloak.Realm);

        var securityScheme = new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.OAuth2,
            Description = "Keycloak OAuth2 Authorization Code flow",
            Flows = new OpenApiOAuthFlows
            {
                AuthorizationCode = new OpenApiOAuthFlow
                {
                    AuthorizationUrl = new Uri(
                        $"{authority}/{AuthConstants.OpenIdConnect.AuthorizationEndpointPath}"
                    ),
                    TokenUrl = new Uri(
                        $"{authority}/{AuthConstants.OpenIdConnect.TokenEndpointPath}"
                    ),
                    Scopes = new Dictionary<string, string>
                    {
                        [AuthConstants.Scopes.OpenId] = "OpenID Connect",
                        [AuthConstants.Scopes.Profile] = "User profile",
                        [AuthConstants.Scopes.Email] = "Email address",
                    },
                },
            },
        };

        var components = document.Components ??= new OpenApiComponents();
        components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();
        components.SecuritySchemes[AuthConstants.OpenApi.OAuth2Scheme] = securityScheme;

        var requirement = new OpenApiSecurityRequirement();
        requirement[
            new OpenApiSecuritySchemeReference(AuthConstants.OpenApi.OAuth2Scheme, document, null)
        ] = [AuthConstants.Scopes.OpenId];

        document.Security ??= [];
        document.Security.Add(requirement);
    }
}
