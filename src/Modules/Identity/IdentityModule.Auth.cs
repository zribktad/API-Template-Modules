using Identity.Auth.Http;
using Identity.Auth.Options;
using Identity.Auth.Security;
using Identity.Auth.Security.ExternalIdentityProviders;
using Identity.Auth.Security.Keycloak;
using Identity.Auth.Security.Sessions;
using Identity.Common.Email;
using Keycloak.AuthServices.Sdk;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using SharedKernel.Application.Resilience;
using SharedKernel.Infrastructure.Configuration;

namespace Identity;

public static partial class IdentityModule
{
    // ── BFF Authentication ────────────────────────────────────────────────────

    private static void RegisterBffAuthentication(
        IServiceCollection services,
        IConfiguration configuration
    )
    {
        KeycloakOptions keycloak =
            configuration.SectionFor<KeycloakOptions>().Get<KeycloakOptions>()
            ?? throw new InvalidOperationException("Keycloak configuration section is missing.");
        BffOptions bff =
            configuration.SectionFor<BffOptions>().Get<BffOptions>() ?? new BffOptions();
        string authority = KeycloakUrlHelper.BuildAuthority(keycloak.AuthServerUrl, keycloak.Realm);

        services
            .AddAuthentication()
            .AddCookie(AuthConstants.BffSchemes.Cookie, options => ConfigureCookie(options, bff))
            .AddOpenIdConnect(
                AuthConstants.BffSchemes.Oidc,
                options => ConfigureOidc(options, keycloak, bff, authority)
            );

        services.AddScoped<CookieSessionRefresher>();
        services.AddSingleton<IBffSessionPrincipalFactory, BffSessionPrincipalFactory>();
        services.AddSingleton<IBffSessionTokenProtector, BffSessionTokenProtector>();
        services.AddSingleton<IBffCsrfTokenService, BffCsrfTokenService>();
        services.AddSingleton<
            IPostConfigureOptions<CookieAuthenticationOptions>,
            BffCookieSecurePostConfigure
        >();
        if (configuration.IsRedisConfigured())
        {
            services.AddSingleton<IBffSessionStore, PostgresCachedBffSessionStore>();
            services.AddSingleton<IBffRefreshCoordinator, RedisBffRefreshCoordinator>();
        }
        else
        {
            services.AddSingleton<IBffSessionStore, PostgresDistributedCacheBffSessionStore>();
            services.AddSingleton<IBffRefreshCoordinator, InProcessBffRefreshCoordinator>();
        }

        services.AddSingleton<BffSessionService>();
        services.AddSingleton<IBffSessionService>(sp => sp.GetRequiredService<BffSessionService>());
        services.AddSingleton<IBffSessionRevocationService>(sp =>
            sp.GetRequiredService<BffSessionService>()
        );
        services.AddScoped<IBffTokenRefreshService, BffTokenRefreshService>();

        services.PostConfigure<JwtBearerOptions>(
            JwtBearerDefaults.AuthenticationScheme,
            options =>
            {
                options.Events ??= new JwtBearerEvents();
                options.Events.OnTokenValidated = IdentityTokenValidatedPipeline.OnTokenValidated;
                options.Events.OnChallenge = JwtBearerAccessDeniedChallenge.OnChallengeAsync;
            }
        );

        services.AddSingleton<RedisTicketStore>();
        services
            .AddOptions<CookieAuthenticationOptions>(AuthConstants.BffSchemes.Cookie)
            .Configure<RedisTicketStore>((opts, store) => opts.SessionStore = store);

        services.AddTransient<IClaimsTransformation, UserPermissionsClaimsTransformation>();

        services
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
                        .RequireClaim(
                            "Permission",
                            SharedKernel.Contracts.Security.Permission.Platform.Manage
                        )
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
                        .RequireClaim(
                            "Permission",
                            SharedKernel.Contracts.Security.Permission.Tenant.Manage,
                            SharedKernel.Contracts.Security.Permission.Platform.Manage
                        )
            );

        services
            .AddHttpClient(
                AuthConstants.HttpClients.KeycloakToken,
                client => client.Timeout = TimeSpan.FromSeconds(10)
            )
            .AddKeycloakHttpRetry(ResiliencePipelineKeys.KeycloakToken);
    }

    private static void ConfigureCookie(CookieAuthenticationOptions options, BffOptions bff)
    {
        options.Cookie.Name = bff.CookieName;
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.Cookie.Path = "/";
        options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
        options.ExpireTimeSpan = TimeSpan.FromMinutes(bff.SessionIdleTimeoutMinutes);
        options.SlidingExpiration = true;
        options.Events.OnRedirectToLogin = context =>
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return Task.CompletedTask;
        };
        options.EventsType = typeof(CookieSessionRefresher);
    }

    private static void ConfigureOidc(
        OpenIdConnectOptions options,
        KeycloakOptions keycloak,
        BffOptions bff,
        string authority
    )
    {
        options.Authority = authority;
        options.RequireHttpsMetadata = KeycloakUrlHelper.ShouldRequireHttpsMetadata(authority);
        options.ClientId = keycloak.Resource;
        options.ClientSecret = keycloak.Credentials.Secret;
        options.ResponseType = OpenIdConnectResponseType.Code;
        options.SaveTokens = true;
        options.SignInScheme = AuthConstants.BffSchemes.Cookie;

        foreach (string scope in bff.Scopes)
        {
            if (!options.Scope.Contains(scope))
                options.Scope.Add(scope);
        }

        options.Events = new OpenIdConnectEvents
        {
            OnTokenValidated = IdentityTokenValidatedPipeline.OnTokenValidated,
            OnAuthenticationFailed = OpenIdConnectAccessDeniedRedirect.OnAuthenticationFailed,
            OnRedirectToIdentityProvider = context =>
            {
                if (
                    context.Properties.Items.TryGetValue(
                        AuthConstants.KeycloakAuthProperties.IdpHint,
                        out string? hint
                    ) && !string.IsNullOrEmpty(hint)
                )
                {
                    context.ProtocolMessage.SetParameter(
                        AuthConstants.KeycloakAuthProperties.IdpHint,
                        hint
                    );
                }
                return Task.CompletedTask;
            },
        };
    }

    // ── Application Services ─────────────────────────────────────────────────

    private static void RegisterApplicationServices(IServiceCollection services)
    {
        services.AddScoped<ISecureTokenGenerator, SecureTokenGenerator>();
        services.AddSingleton<IKeycloakService, KeycloakService>();
        services.AddSingleton<IExternalIdentityProvider, GoogleIdentityProvider>();
    }

    // ── Keycloak Admin ────────────────────────────────────────────────────────

    private static void RegisterKeycloakAdmin(IServiceCollection services)
    {
        services
            .AddOptions<KeycloakAdminClientOptions>()
            .Configure<IOptions<KeycloakOptions>>(
                (adminOpts, keycloakOpts) =>
                {
                    adminOpts.AuthServerUrl = keycloakOpts.Value.AuthServerUrl;
                    adminOpts.Realm = keycloakOpts.Value.Realm;
                }
            );

        services.AddSingleton<KeycloakAdminTokenProvider>();
        services.AddTransient<KeycloakAdminTokenHandler>();

        services
            .AddKeycloakAdminHttpClient(_ => { })
            .AddHttpMessageHandler<KeycloakAdminTokenHandler>()
            .AddKeycloakHttpRetry(ResiliencePipelineKeys.KeycloakAdmin);

        services.AddScoped<IKeycloakAdminService, KeycloakAdminService>();
        services.AddScoped<IKeycloakAndBffGlobalLogoutService, KeycloakAndBffGlobalLogoutService>();
    }
}
