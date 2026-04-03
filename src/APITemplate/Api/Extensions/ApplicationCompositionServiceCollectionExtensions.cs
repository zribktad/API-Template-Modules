using System.Security.Claims;
using System.Text.Json;
using APITemplate.Api.Authorization;
using APITemplate.Api.Security;
using Asp.Versioning;
using Identity.Application.Common.Security;
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
        services.AddScoped<IActorProvider, HttpActorProvider>();

        services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.Authority = keycloakAuthority;
                options.Audience = keycloakAudience;
                options.RequireHttpsMetadata = !keycloakAuthority.StartsWith(
                    "http://",
                    StringComparison.OrdinalIgnoreCase
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
                options.Events = new JwtBearerEvents
                {
                    OnTokenValidated = context =>
                    {
                        if (context.Principal?.Identity is ClaimsIdentity identity)
                        {
                            MapKeycloakClaims(identity);
                        }

                        return Task.CompletedTask;
                    },
                };
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

        return $"{authServerUrl.TrimEnd('/')}/realms/{realm}";
    }

    private static void MapKeycloakClaims(ClaimsIdentity identity)
    {
        if (
            identity.FindFirst(ClaimTypes.Name) is null
            && identity.FindFirst(AuthConstants.Claims.PreferredUsername) is Claim preferredUsername
        )
        {
            identity.AddClaim(new Claim(ClaimTypes.Name, preferredUsername.Value));
        }

        if (identity.FindFirst(AuthConstants.Claims.RealmAccess) is not Claim realmAccess)
        {
            return;
        }

        using JsonDocument document = JsonDocument.Parse(realmAccess.Value);
        if (
            !document.RootElement.TryGetProperty(AuthConstants.Claims.Roles, out JsonElement roles)
            || roles.ValueKind != JsonValueKind.Array
        )
        {
            return;
        }

        foreach (JsonElement role in roles.EnumerateArray())
        {
            string? roleValue = role.GetString();
            if (
                string.IsNullOrWhiteSpace(roleValue)
                || identity.HasClaim(ClaimTypes.Role, roleValue)
            )
            {
                continue;
            }

            identity.AddClaim(new Claim(ClaimTypes.Role, roleValue));
        }
    }
}
