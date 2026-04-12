using System.Security.Claims;
using APITemplate.Api.Authorization;
using APITemplate.Api.Security;
using Asp.Versioning;
using Identity.Auth.Security;
using Identity.Auth.Security.Keycloak;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.IdentityModel.Tokens;
using SharedKernel.Application.Context;

namespace APITemplate.Api.Extensions;

public static class ApplicationCompositionServiceCollectionExtensions
{
    public static IServiceCollection AddApplicationComposition(
        this IServiceCollection services,
        IConfiguration configuration
    )
    {
        string keycloakAuthority = BuildKeycloakAuthority(configuration);
        string? keycloakAudience = configuration["Keycloak:resource"];
        bool validateAudience = configuration.GetValue("Keycloak:verify-token-audience", true);

        services.AddHttpContextAccessor();
        services.AddScoped<ITenantProvider, HttpTenantProvider>();
        services.AddScoped<HttpRequestIdentityProvider>();
        services.AddScoped<ICurrentRequestUser>(sp =>
            sp.GetRequiredService<HttpRequestIdentityProvider>()
        );
        services.AddScoped<IActorProvider>(sp =>
            sp.GetRequiredService<HttpRequestIdentityProvider>()
        );

        services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.Authority = keycloakAuthority;
                options.Audience = keycloakAudience;
                options.RequireHttpsMetadata = KeycloakUrlHelper.ShouldRequireHttpsMetadata(
                    keycloakAuthority
                );
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = validateAudience,
                    ValidAudience = keycloakAudience,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    NameClaimType = ClaimTypes.Name,
                    RoleClaimType = ClaimTypes.Role,
                };
                // OnTokenValidated is assigned in IdentityModule via PostConfigure<JwtBearerOptions>
                // (IdentityTokenValidatedPipeline + KeycloakClaimMapper).
            });
        services.AddAuthorization();
        services.AddSingleton<IRolePermissionMap, StaticRolePermissionMap>();
        services.AddSingleton<IAuthorizationHandler, PermissionAuthorizationHandler>();
        services.AddSingleton<IAuthorizationPolicyProvider, PermissionPolicyProvider>();

        services
            .AddApiVersioning(options =>
            {
                options.DefaultApiVersion = new ApiVersion(1, 0);
                options.AssumeDefaultVersionWhenUnspecified = true;
                options.ReportApiVersions = true;
                options.ApiVersionReader = new UrlSegmentApiVersionReader();
            })
            .AddApiExplorer(options =>
            {
                options.GroupNameFormat = "'v'VVV";
                options.SubstituteApiVersionInUrl = true;
            });

        return services;
    }

    private static string BuildKeycloakAuthority(IConfiguration configuration)
    {
        string? authServerUrl = configuration["Keycloak:auth-server-url"];
        string? realm = configuration["Keycloak:realm"];

        if (string.IsNullOrWhiteSpace(authServerUrl) || string.IsNullOrWhiteSpace(realm))
        {
            throw new InvalidOperationException(
                "Keycloak authentication requires both 'Keycloak:auth-server-url' and 'Keycloak:realm'."
            );
        }

        return KeycloakUrlHelper.BuildAuthority(authServerUrl, realm);
    }
}
