using Identity.Options;
using Identity.Security;
using Identity.Security.Keycloak;
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

    public async Task TransformAsync(
        OpenApiDocument document,
        OpenApiDocumentTransformerContext context,
        CancellationToken cancellationToken
    )
    {
        IEnumerable<AuthenticationScheme> schemes = await _schemeProvider.GetAllSchemesAsync();
        if (!schemes.Any(s => s.Name == JwtBearerDefaults.AuthenticationScheme))
            return;

        string authority = KeycloakUrlHelper.BuildAuthority(
            _keycloak.AuthServerUrl,
            _keycloak.Realm
        );

        OpenApiSecurityScheme securityScheme = new()
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

        OpenApiComponents components = document.Components ??= new OpenApiComponents();
        components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();
        components.SecuritySchemes[AuthConstants.OpenApi.OAuth2Scheme] = securityScheme;

        OpenApiSecurityRequirement requirement = new();
        requirement[
            new OpenApiSecuritySchemeReference(AuthConstants.OpenApi.OAuth2Scheme, document, null)
        ] = [AuthConstants.Scopes.OpenId];

        document.Security ??= [];
        document.Security.Add(requirement);
    }
}
