using APITemplate.Api.Authorization;
using APITemplate.Application.Common.Options;
using APITemplate.Application.Common.Resilience;
using APITemplate.Application.Common.Security;
using APITemplate.Domain.Enums;
using APITemplate.Infrastructure.Health;
using APITemplate.Infrastructure.Observability;
using APITemplate.Infrastructure.Security;
using Keycloak.AuthServices.Authorization;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using Polly;

namespace APITemplate.Api.Extensions;

/// <summary>
/// Presentation-layer extension class that configures all authentication and authorization
/// services including Keycloak JWT, BFF cookie/OIDC flows, CORS, and per-permission policies.
/// </summary>
public static class AuthenticationServiceCollectionExtensions
{
    /// <summary>
    /// Registers and validates CORS, BFF, system-identity, bootstrap-tenant, and Keycloak
    /// options from configuration without yet configuring any authentication schemes.
    /// </summary>
    public static IServiceCollection AddAuthenticationOptions(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment
    )
    {
        var corsSection = configuration.SectionFor<CorsOptions>();
        services.AddValidatedOptions<CorsOptions>(configuration);

        var corsOrigins = (corsSection.Get<CorsOptions>() ?? new CorsOptions())
            .AllowedOrigins.Where(origin => !string.IsNullOrWhiteSpace(origin))
            .Select(origin => origin.Trim())
            .ToArray();

        if (corsOrigins?.Length > 0)
        {
            services.AddCors(options =>
            {
                options.AddDefaultPolicy(policy =>
                {
                    policy
                        .WithOrigins(corsOrigins)
                        .AllowAnyHeader()
                        .AllowAnyMethod()
                        .AllowCredentials();
                });
            });
        }

        services.AddValidatedOptions<BffOptions>(configuration);

        services.AddValidatedOptions<SystemIdentityOptions>(
            configuration,
            validateDataAnnotations: false
        );

        services
            .AddValidatedOptions<BootstrapTenantOptions>(configuration)
            .Validate(
                o => !string.IsNullOrWhiteSpace(o.Code) && !string.IsNullOrWhiteSpace(o.Name),
                "Bootstrap tenant code/name is required"
            );

        services.AddValidatedOptions<KeycloakOptions>(configuration);

        return services;
    }

    /// <summary>
    /// Registers the full hybrid authentication pipeline: JWT bearer for API clients,
    /// cookie + OIDC for browser BFF clients, session ticket store, and all per-permission
    /// authorization policies mapped from <see cref="Permission.All"/>.
    /// </summary>
    public static IServiceCollection AddKeycloakBffAuthentication(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment
    )
    {
        var authSettings = BuildAuthSettings(configuration);

        ConfigureAuthenticationSchemes(services, authSettings, environment);
        ConfigureCookieSessionStore(services);
        ConfigureAuthorization(services, configuration);
        ConfigureKeycloakInfrastructure(services, configuration);

        return services;
    }

    private static AuthSettings BuildAuthSettings(IConfiguration configuration)
    {
        var keycloak =
            configuration.SectionFor<KeycloakOptions>().Get<KeycloakOptions>()
            ?? throw new InvalidOperationException("Keycloak configuration section is missing.");
        var bffOptions =
            configuration.SectionFor<BffOptions>().Get<BffOptions>() ?? new BffOptions();
        var authority = KeycloakUrlHelper.BuildAuthority(keycloak.AuthServerUrl, keycloak.Realm);
        return new AuthSettings(keycloak, bffOptions, authority);
    }

    private static void ConfigureAuthenticationSchemes(
        IServiceCollection services,
        AuthSettings settings,
        IHostEnvironment environment
    )
    {
        services
            .AddAuthentication(options =>
            {
                options.DefaultScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
            })
            .AddJwtBearer(options => ConfigureJwtBearer(options, settings, environment))
            .AddCookie(
                AuthConstants.BffSchemes.Cookie,
                options => ConfigureCookie(options, settings, environment)
            )
            .AddOpenIdConnect(
                AuthConstants.BffSchemes.Oidc,
                options => ConfigureOpenIdConnect(options, settings, environment)
            );
    }

    private static void ConfigureJwtBearer(
        JwtBearerOptions options,
        AuthSettings settings,
        IHostEnvironment environment
    )
    {
        var isDevelopment = environment.IsDevelopment();

        options.Authority = settings.Authority;
        options.Audience = settings.Keycloak.Resource;
        options.RequireHttpsMetadata = !isDevelopment;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            LogTokenId = isDevelopment,
            LogValidationExceptions = isDevelopment,
            RequireExpirationTime = true,
            RequireSignedTokens = true,
            RequireAudience = true,
            SaveSigninToken = false,
            TryAllDecryptionKeys = true,
            TryAllIssuerSigningKeys = true,
            ValidateActor = false,
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateIssuerSigningKey = true,
            ValidateLifetime = true,
            ValidateTokenReplay = false,
            ClockSkew = TimeSpan.FromMinutes(5),
        };
        options.Events = new JwtBearerEvents
        {
            OnTokenValidated = TenantClaimValidator.OnTokenValidated,
        };
    }

    private static void ConfigureCookie(
        CookieAuthenticationOptions options,
        AuthSettings settings,
        IHostEnvironment environment
    )
    {
        options.Cookie.Name = settings.Bff.CookieName;
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.Cookie.SecurePolicy = environment.IsDevelopment()
            ? CookieSecurePolicy.SameAsRequest
            : CookieSecurePolicy.Always;
        options.ExpireTimeSpan = TimeSpan.FromMinutes(settings.Bff.SessionTimeoutMinutes);
        options.SlidingExpiration = true;
        options.Events.OnRedirectToLogin = RejectUnauthorizedRedirectAsync;
        options.Events.OnValidatePrincipal = CookieSessionRefresher.OnValidatePrincipal;
    }

    private static void ConfigureOpenIdConnect(
        OpenIdConnectOptions options,
        AuthSettings settings,
        IHostEnvironment environment
    )
    {
        options.Authority = settings.Authority;
        options.RequireHttpsMetadata = !environment.IsDevelopment();
        options.ClientId = settings.Keycloak.Resource;
        options.ClientSecret = settings.Keycloak.Credentials.Secret;
        options.ResponseType = OpenIdConnectResponseType.Code;
        options.SaveTokens = true;
        options.SignInScheme = AuthConstants.BffSchemes.Cookie;

        options.Scope.Clear();
        foreach (var scope in settings.Bff.Scopes)
            options.Scope.Add(scope);

        options.Events = new OpenIdConnectEvents
        {
            OnTokenValidated = TenantClaimValidator.OnTokenValidated,
        };
    }

    private static void ConfigureCookieSessionStore(IServiceCollection services)
    {
        services.AddSingleton<DragonflyTicketStore>();
        services
            .AddOptions<CookieAuthenticationOptions>(AuthConstants.BffSchemes.Cookie)
            .Configure<DragonflyTicketStore>((opts, store) => opts.SessionStore = store);
    }

    private static void ConfigureAuthorization(
        IServiceCollection services,
        IConfiguration configuration
    )
    {
        services.AddSingleton<IRolePermissionMap, StaticRolePermissionMap>();
        services.AddSingleton<IAuthorizationHandler, PermissionAuthorizationHandler>();

        services
            .AddKeycloakAuthorization(configuration)
            .AddAuthorizationBuilder()
            .SetFallbackPolicy(
                new AuthorizationPolicyBuilder()
                    .AddAuthenticationSchemes(
                        JwtBearerDefaults.AuthenticationScheme,
                        AuthConstants.BffSchemes.Cookie
                    )
                    .RequireAuthenticatedUser()
                    .Build()
            )
            .AddPolicy(
                AuthConstants.Policies.PlatformAdmin,
                policy =>
                    policy
                        .AddAuthenticationSchemes(
                            JwtBearerDefaults.AuthenticationScheme,
                            AuthConstants.BffSchemes.Cookie
                        )
                        .RequireAuthenticatedUser()
                        .RequireRole(UserRole.PlatformAdmin.ToString())
            )
            .AddPolicy(
                AuthConstants.Policies.TenantAdmin,
                policy =>
                    policy
                        .AddAuthenticationSchemes(
                            JwtBearerDefaults.AuthenticationScheme,
                            AuthConstants.BffSchemes.Cookie
                        )
                        .RequireAuthenticatedUser()
                        .RequireRole(
                            UserRole.TenantAdmin.ToString(),
                            UserRole.PlatformAdmin.ToString()
                        )
            );

        services.AddSingleton<IAuthorizationPolicyProvider, PermissionPolicyProvider>();
    }

    private static void ConfigureKeycloakInfrastructure(
        IServiceCollection services,
        IConfiguration configuration
    )
    {
        services.AddHttpClient(nameof(KeycloakHealthCheck));
        services.AddHttpClient(AuthConstants.HttpClients.KeycloakToken);
        services
            .AddHealthChecks()
            .AddCheck<KeycloakHealthCheck>(HealthCheckNames.Keycloak, tags: ["identity"]);

        var keycloakOptions = configuration.SectionFor<KeycloakOptions>().Get<KeycloakOptions>()!;

        services.AddResiliencePipeline(
            ResiliencePipelineKeys.KeycloakReadiness,
            builder =>
            {
                builder.AddRetry(
                    new()
                    {
                        MaxRetryAttempts = keycloakOptions.ReadinessMaxRetries - 1,
                        BackoffType = DelayBackoffType.Constant,
                        Delay = TimeSpan.FromSeconds(2),
                        ShouldHandle = new PredicateBuilder()
                            .Handle<HttpRequestException>()
                            .Handle<TaskCanceledException>(),
                    }
                );
            }
        );
    }

    private static Task RejectUnauthorizedRedirectAsync(
        Microsoft.AspNetCore.Authentication.RedirectContext<CookieAuthenticationOptions> context
    )
    {
        AuthTelemetry.RecordUnauthorizedRedirect();
        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        return Task.CompletedTask;
    }

    /// <summary>Immutable carrier for the Keycloak options and derived authority URL used during scheme configuration.</summary>
    private sealed record AuthSettings(KeycloakOptions Keycloak, BffOptions Bff, string Authority);
}
