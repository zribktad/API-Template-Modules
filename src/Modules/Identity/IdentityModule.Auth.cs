using System.Security.Claims;
using BuildingBlocks.Application.Configuration;
using BuildingBlocks.Application.Resilience;
using Identity.Auth.Http;
using Identity.Auth.Options;
using Identity.Auth.Security.Keycloak;
using Identity.Auth.Security.Sessions;
using Identity.Auth.Security.Sessions.Lifecycle;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;

namespace Identity;

public static partial class IdentityModule
{
    // ── Authentication ────────────────────────────────────────────────────────

    /// <summary>
    ///     Entry point for all authentication registration: sets up all schemes in a single fluent
    ///     chain, then configures JWT Bearer, BFF cookie/OIDC, session infrastructure, and policies.
    /// </summary>
    private static void RegisterAuthentication(
        IServiceCollection services,
        IConfiguration configuration
    )
    {
        services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer()
            .AddCookie(AuthConstants.BffSchemes.Cookie)
            .AddOpenIdConnect(AuthConstants.BffSchemes.Oidc, _ => { })
            .AddScheme<
                KeycloakWebhookAuthenticationSchemeOptions,
                KeycloakWebhookAuthenticationHandler
            >(AuthConstants.WebhookSchemes.KeycloakEvent, _ => { });

        ConfigureJwtBearerOptions(services);
        ConfigureBffOptions(services);
        AddBffSessionInfrastructure(services, configuration);
        AddBffSessionServices(services);
        AddAuthorizationPolicies(services);
        AddKeycloakTokenHttpClient(services);
    }

    /// <summary>
    ///     Configures JWT Bearer options and wires the
    ///     <see cref="IdentityTokenValidatedPipeline" /> and challenge handler via
    ///     <c>PostConfigure</c> so they run after all options are fully settled.
    /// </summary>
    private static void ConfigureJwtBearerOptions(IServiceCollection services)
    {
        services
            .AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<IOptions<KeycloakOptions>, ILoggerFactory>(
                (options, keycloakOpts, loggerFactory) =>
                {
                    KeycloakOptions keycloak = keycloakOpts.Value;
                    string authority = KeycloakUrlHelper.BuildAuthority(
                        keycloak.AuthServerUrl,
                        keycloak.Realm
                    );
                    options.Authority = authority;
                    options.Audience = keycloak.Resource;
                    options.RequireHttpsMetadata = KeycloakUrlHelper.ShouldRequireHttpsMetadata(
                        authority
                    );
                    WarnIfPlainHttp(loggerFactory, "JWT Bearer", authority);
                    options.TokenValidationParameters = new TokenValidationParameters
                    {
                        ValidateIssuer = true,
                        ValidateAudience = keycloak.VerifyTokenAudience,
                        ValidAudience = keycloak.Resource,
                        ValidateLifetime = true,
                        ValidateIssuerSigningKey = true,
                        NameClaimType = ClaimTypes.Name,
                        RoleClaimType = ClaimTypes.Role,
                    };
                }
            );

        // PostConfigure runs after all AddJwtBearer calls so options are fully settled before
        // attaching the token-validated pipeline and challenge handler.
        services.PostConfigure<JwtBearerOptions>(
            JwtBearerDefaults.AuthenticationScheme,
            options =>
            {
                options.Events ??= new JwtBearerEvents();
                options.Events.OnTokenValidated = IdentityTokenValidatedPipeline.OnTokenValidated;
                // Returns 401 with a structured ProblemDetails body instead of the default 401 redirect.
                options.Events.OnChallenge = JwtBearerAccessDeniedChallenge.OnChallengeAsync;
            }
        );
    }

    /// <summary>
    ///     Configures BFF Cookie and OIDC options and the services they depend on.
    ///     The Cookie handler issues an opaque <c>HttpOnly</c> session cookie; the OIDC handler drives
    ///     the Authorization Code flow against Keycloak. <see cref="BffCookieSecurePostConfigure" />
    ///     enforces <c>Secure</c>-only cookies outside of development environments.
    /// </summary>
    private static void ConfigureBffOptions(IServiceCollection services)
    {
        services
            .AddOptions<CookieAuthenticationOptions>(AuthConstants.BffSchemes.Cookie)
            .Configure<IOptions<BffOptions>>(
                (options, bffOpts) => ConfigureCookie(options, bffOpts.Value)
            );

        services
            .AddOptions<OpenIdConnectOptions>(AuthConstants.BffSchemes.Oidc)
            .Configure<IOptions<KeycloakOptions>, IOptions<BffOptions>, ILoggerFactory>(
                (options, keycloakOpts, bffOpts, loggerFactory) =>
                {
                    string authority = KeycloakUrlHelper.BuildAuthority(
                        keycloakOpts.Value.AuthServerUrl,
                        keycloakOpts.Value.Realm
                    );
                    WarnIfPlainHttp(loggerFactory, "OIDC", authority);
                    ConfigureOidc(options, keycloakOpts.Value, bffOpts.Value, authority);
                }
            );

        services.AddScoped<CookieSessionRefresher>();
        services.AddSingleton<IBffSessionPrincipalFactory, BffSessionPrincipalFactory>();
        services.AddSingleton<IBffSessionTokenProtector, BffSessionTokenProtector>();
        services.AddSingleton<IBffSessionDbContextFactory, ScopedBffSessionDbContextFactory>();
        services.AddSingleton<IBffCsrfTokenService, BffCsrfTokenService>();
        services.AddSingleton<
            IPostConfigureOptions<CookieAuthenticationOptions>,
            BffCookieSecurePostConfigure
        >();
    }

    /// <summary>
    ///     Registers the two-tier BFF session storage stack and the ASP.NET Core server-side ticket store.
    ///     <para>
    ///         <b>L1 - in-process:</b> <see cref="BffLocalSessionCache" /> keeps a short-lived local
    ///         copy to avoid a distributed lookup on repeated requests for the same session.
    ///     </para>
    ///     <para>
    ///         <b>L2 - distributed:</b> Redis deployments use <see cref="PostgresCachedBffSessionStore" />
    ///         for read-through caching over PostgreSQL. Non-Redis deployments keep PostgreSQL as the
    ///         source of truth and use the configured <see cref="Microsoft.Extensions.Caching.Distributed.IDistributedCache" />
    ///         provider through <see cref="PostgresDistributedCacheBffSessionStore" />.
    ///     </para>
    ///     <para>
    ///         <b>L1 coherence:</b> <see cref="CachingBffSessionStoreDecorator" /> wraps either L2
    ///         store. With Redis, <see cref="BffSessionRevocationSubscriber" /> listens for revocation
    ///         broadcasts and evicts stale local entries on every API instance. Without Redis, local
    ///         staleness is bounded by <see cref="BffOptions.LocalCacheTtlSeconds" />.
    ///     </para>
    ///     <para>
    ///         <b>Refresh coordination:</b> Redis deployments use <see cref="RedisBffRefreshCoordinator" />
    ///         so only one instance refreshes a browser session at a time; single-process deployments
    ///         use <see cref="InProcessBffRefreshCoordinator" />.
    ///     </para>
    ///     <para>
    ///         <b>Ticket store:</b> <see cref="RedisTicketStore" /> keeps authentication ticket
    ///         contents server-side so the browser cookie contains only an opaque key.
    ///     </para>
    /// </summary>
    private static void AddBffSessionInfrastructure(
        IServiceCollection services,
        IConfiguration configuration
    )
    {
        // L1: per-instance cache that skips the L2 round-trip for repeat reads of the same session.
        services.AddSingleton<IBffLocalSessionCache, BffLocalSessionCache>();

        if (configuration.IsRedisConfigured())
        {
            // L2: PostgreSQL primary store + Redis read-through cache.
            services.AddSingleton<PostgresCachedBffSessionStore>();
            // Publishes session mutations so peer instances can evict stale L1 entries promptly.
            services.AddSingleton<
                IBffSessionRevocationNotifier,
                RedisBffSessionRevocationNotifier
            >();
            // Layers L1 over L2; cache hits skip the store, mutations update or invalidate L1.
            RegisterDecoratedStore<PostgresCachedBffSessionStore>(services);

            // Distributed refresh lock prevents concurrent token refreshes across API instances.
            services.AddSingleton<IBffRefreshCoordinator, RedisBffRefreshCoordinator>();
            // Redis pub/sub subscriber keeps L1 entries coherent across instances.
            services.AddHostedService<BffSessionRevocationSubscriber>();
        }
        else
        {
            // L2 fallback: PostgreSQL primary store + configured IDistributedCache provider.
            services.AddSingleton<PostgresDistributedCacheBffSessionStore>();
            // No Redis means no cross-instance broadcast; L1 staleness is bounded by local cache TTL.
            services.AddSingleton<
                IBffSessionRevocationNotifier,
                NullBffSessionRevocationNotifier
            >();
            // Keep the same local-cache decorator shape for Redis and non-Redis deployments.
            RegisterDecoratedStore<PostgresDistributedCacheBffSessionStore>(services);
            // In-process refresh coordination is sufficient only for single-instance deployments.
            services.AddSingleton<IBffRefreshCoordinator, InProcessBffRefreshCoordinator>();
        }

        // Stores ASP.NET Core authentication tickets server-side; token data never reaches the browser.
        services.AddSingleton<RedisTicketStore>();
        services
            .AddOptions<CookieAuthenticationOptions>(AuthConstants.BffSchemes.Cookie)
            .Configure<RedisTicketStore>((opts, store) => opts.SessionStore = store);
    }

    private static void RegisterDecoratedStore<TL2>(IServiceCollection services)
        where TL2 : class, IBffSessionStore
    {
        services.AddSingleton<IBffSessionStore>(sp => new CachingBffSessionStoreDecorator(
            sp.GetRequiredService<TL2>(),
            sp.GetRequiredService<IBffLocalSessionCache>(),
            sp.GetRequiredService<IBffSessionRevocationNotifier>(),
            sp.GetRequiredService<ILogger<CachingBffSessionStoreDecorator>>()
        ));
    }

    /// <summary>
    ///     Registers BFF session application-layer services.
    ///     <see cref="BffSessionService" /> is the central coordinator and is intentionally exposed
    ///     through focused interfaces so consumers depend only on the operations they use.
    ///     <see cref="IBffTokenRefreshService" /> is scoped because it participates in the request
    ///     pipeline through <see cref="CookieSessionRefresher" />.
    /// </summary>
    private static void AddBffSessionServices(IServiceCollection services)
    {
        // Enforces session business rules such as expiry and terminal status.
        services.AddSingleton<IBffSessionValidator, BffSessionValidator>();
        // Builds complete BffSessionRecord instances from authentication tickets.
        services.AddSingleton<IBffSessionRecordFactory, BffSessionRecordFactory>();
        // Applies refresh, revoke, and expire transitions through optimistic concurrency.
        services.AddSingleton<IBffSessionMutator, BffSessionMutator>();

        // Register the concrete coordinator once, then project it through narrower interfaces.
        services.AddSingleton<BffSessionService>();
        // Exposed for read/write session operations.
        services.AddSingleton<IBffSessionService>(sp => sp.GetRequiredService<BffSessionService>());
        // Exposed for logout, password-change, and revoke-all workflows.
        services.AddSingleton<IBffSessionRevocationService>(sp =>
            sp.GetRequiredService<BffSessionService>()
        );

        // Token refresh participates in the request pipeline via CookieSessionRefresher.
        services.AddScoped<IBffTokenRefreshService, BffTokenRefreshService>();
    }

    /// <summary>
    ///     Registers permission infrastructure, claims transformation, and authorization policies.
    ///     Applies to both JWT Bearer and Cookie schemes.
    /// </summary>
    private static void AddAuthorizationPolicies(IServiceCollection services)
    {
        // Evaluates permission claims that UserPermissionsClaimsTransformation adds to the principal.
        services.AddSingleton<IAuthorizationHandler, PermissionAuthorizationHandler>();
        // Builds per-permission policies on demand so [Authorize("Orders.Read")] style attributes work without pre-registering each policy.
        services.AddSingleton<IAuthorizationPolicyProvider, PermissionPolicyProvider>();
        // Translates Keycloak realm roles into application-level permission claims on every authenticated request.
        services.AddTransient<IClaimsTransformation, UserPermissionsClaimsTransformation>();

        services
            .AddAuthorizationBuilder()
            // Default policy: any authenticated user on either scheme can access unlocked endpoints.
            .SetFallbackPolicy(
                new AuthorizationPolicyBuilder()
                    .AddAuthenticationSchemes(
                        JwtBearerDefaults.AuthenticationScheme,
                        AuthConstants.BffSchemes.Cookie
                    )
                    .RequireAuthenticatedUser()
                    .Build()
            )
            // Requires the Platform.Manage permission claim — granted only to platform-level admins.
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
                            AuthConstants.Claims.Permission,
                            BuildingBlocks.Security.Permission.Platform.Manage
                        )
            )
            // Accepts Tenant.Manage OR Platform.Manage — platform admins implicitly have tenant admin rights.
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
                            AuthConstants.Claims.Permission,
                            BuildingBlocks.Security.Permission.Tenant.Manage,
                            BuildingBlocks.Security.Permission.Platform.Manage
                        )
            );
    }

    /// <summary>
    ///     Registers the named <see cref="System.Net.Http.HttpClient" /> used by
    ///     <see cref="BffTokenRefreshService" /> to exchange refresh tokens at Keycloak's token
    ///     endpoint. A 10-second timeout is applied together with the project-standard Keycloak retry
    ///     pipeline (<see cref="ResiliencePipelineKeys.KeycloakToken" />).
    /// </summary>
    private static void AddKeycloakTokenHttpClient(IServiceCollection services)
    {
        services
            .AddHttpClient(
                AuthConstants.HttpClients.KeycloakToken,
                client => client.Timeout = TimeSpan.FromSeconds(10)
            )
            .AddKeycloakHttpRetry(ResiliencePipelineKeys.KeycloakToken);
    }

    /// <summary>
    ///     Configures the BFF Cookie handler. The cookie is <c>HttpOnly</c> and uses
    ///     <c>SameSite=Lax</c> to allow the Keycloak redirect to complete without being blocked.
    ///     The idle timeout slides on every active request up to
    ///     <see cref="BffOptions.SessionIdleTimeoutMinutes" />. <see cref="CookieSessionRefresher" />
    ///     is set as the events handler to enable silent token refresh before the cookie expires.
    /// </summary>
    private static void ConfigureCookie(CookieAuthenticationOptions options, BffOptions bff)
    {
        options.Cookie.Name = bff.CookieName;
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.Cookie.Path = "/";
        options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
        options.ExpireTimeSpan = TimeSpan.FromMinutes(bff.SessionIdleTimeoutMinutes);
        options.SlidingExpiration = true;
        options.EventsType = typeof(CookieSessionRefresher);
    }

    /// <summary>
    ///     Configures the OIDC handler for the Keycloak Authorization Code flow. Tokens are saved
    ///     so the BFF session store can persist and encrypt them server-side. The <c>kc_idp_hint</c>
    ///     query parameter is forwarded when the caller sets
    ///     <see cref="AuthConstants.KeycloakAuthProperties.IdpHint" /> in the authentication
    ///     properties, enabling direct external-IdP login from the
    ///     <c>POST /api/v1/bff/login/{idpHint}</c> endpoint.
    /// </summary>
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

    private static void WarnIfPlainHttp(
        ILoggerFactory loggerFactory,
        string scheme,
        string authority
    )
    {
        if (!KeycloakUrlHelper.ShouldRequireHttpsMetadata(authority))
            loggerFactory
                .CreateLogger("Identity.Authentication")
                .LogWarning(
                    "{Scheme}: Keycloak authority {Authority} uses plain HTTP — HTTPS metadata validation is disabled. Do not use HTTP in production.",
                    scheme,
                    authority
                );
    }
}
