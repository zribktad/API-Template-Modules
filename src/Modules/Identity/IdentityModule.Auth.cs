using Identity.Auth.Http;
using Identity.Auth.Options;
using Identity.Auth.Security;
using Identity.Auth.Security.ExternalIdentityProviders;
using Identity.Auth.Security.Keycloak;
using Identity.Auth.Security.Sessions;
using Identity.Auth.Security.Sessions.Lifecycle;
using Identity.Common.Email;
using Identity.Directory.Domain.Services;
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

    /// <summary>
    ///     Entry point for BFF authentication registration. Resolves configuration, then delegates
    ///     each concern to a focused helper so the registration stays readable as the feature grows.
    /// </summary>
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

        AddBffAuthenticationSchemes(services, keycloak, bff, authority);
        AddBffSessionInfrastructure(services, configuration);
        AddBffSessionServices(services);
        AddBffAuthorizationPolicies(services);
        AddKeycloakTokenHttpClient(services);
    }

    /// <summary>
    ///     Registers the BFF Cookie and OIDC authentication handlers and the services they depend on.
    ///     The Cookie handler issues an opaque <c>HttpOnly</c> session cookie; the OIDC handler drives
    ///     the Authorization Code flow against Keycloak. <see cref="BffCookieSecurePostConfigure" />
    ///     enforces <c>Secure</c>-only cookies outside of development environments.
    /// </summary>
    private static void AddBffAuthenticationSchemes(
        IServiceCollection services,
        KeycloakOptions keycloak,
        BffOptions bff,
        string authority
    )
    {
        services
            .AddAuthentication()
            // Issues the HttpOnly session cookie; session data lives server-side in RedisTicketStore.
            .AddCookie(AuthConstants.BffSchemes.Cookie, options => ConfigureCookie(options, bff))
            // Drives the Keycloak Authorization Code flow and hands tokens to the session store.
            .AddOpenIdConnect(
                AuthConstants.BffSchemes.Oidc,
                options => ConfigureOidc(options, keycloak, bff, authority)
            );

        // Hooks into CookieAuthenticationEvents to silently refresh the access token before expiry.
        services.AddScoped<CookieSessionRefresher>();
        // Builds a ClaimsPrincipal from a stored BffSessionRecord (used after a successful cookie validation).
        services.AddSingleton<IBffSessionPrincipalFactory, BffSessionPrincipalFactory>();
        // Encrypts/decrypts access, refresh, and id tokens stored in the session record using IDataProtector.
        services.AddSingleton<IBffSessionTokenProtector, BffSessionTokenProtector>();
        // Generates and validates the double-submit CSRF token returned by GET /api/v1/bff/csrf.
        services.AddSingleton<IBffCsrfTokenService, BffCsrfTokenService>();
        // Post-configure hook that flips SecurePolicy to Always in non-development environments.
        services.AddSingleton<
            IPostConfigureOptions<CookieAuthenticationOptions>,
            BffCookieSecurePostConfigure
        >();
    }

    /// <summary>
    ///     Registers the two-tier BFF session storage stack and the ASP.NET Core server-side ticket store.
    ///     <para>
    ///         <b>L1 — in-process:</b> <see cref="BffLocalSessionCache" /> (<see cref="Microsoft.Extensions.Caching.Memory.IMemoryCache" />-backed,
    ///         TTL from <see cref="BffOptions.LocalCacheTtlSeconds" />) eliminates Redis round-trips for
    ///         repeated reads of the same session within a single instance.
    ///     </para>
    ///     <para>
    ///         <b>L2 — distributed:</b> when Redis is configured, <see cref="PostgresCachedBffSessionStore" />
    ///         (PostgreSQL primary + Redis read-through cache) is used; otherwise
    ///         <see cref="PostgresDistributedCacheBffSessionStore" /> falls back to the distributed cache
    ///         provider without Redis-specific features.
    ///     </para>
    ///     <para>
    ///         <b>L1 coherence:</b> <see cref="CachingBffSessionStoreDecorator" /> layers L1 over L2 and
    ///         publishes revocation events via <see cref="RedisBffSessionRevocationNotifier" /> (or the
    ///         no-op <see cref="NullBffSessionRevocationNotifier" /> when Redis is absent).
    ///         <see cref="BffSessionRevocationSubscriber" /> listens to those events as an
    ///         <see cref="Microsoft.Extensions.Hosting.IHostedService" /> and invalidates the local
    ///         cache entry on every instance, bounding cross-instance staleness to milliseconds rather
    ///         than the full <see cref="BffOptions.LocalCacheTtlSeconds" /> window.
    ///     </para>
    ///     <para>
    ///         <b>Refresh coordination:</b> <see cref="RedisBffRefreshCoordinator" /> uses a distributed
    ///         lock so only one instance refreshes a given token concurrently;
    ///         <see cref="InProcessBffRefreshCoordinator" /> is used as a single-instance fallback.
    ///     </para>
    ///     <para>
    ///         <b>Ticket store:</b> <see cref="RedisTicketStore" /> is wired as the ASP.NET Core cookie
    ///         session store so the browser cookie holds only an opaque key — no token data ever leaves
    ///         the server.
    ///     </para>
    /// </summary>
    private static void AddBffSessionInfrastructure(
        IServiceCollection services,
        IConfiguration configuration
    )
    {
        // L1: per-instance IMemoryCache that skips the Redis round-trip for repeat reads of the same session.
        services.AddSingleton<IBffLocalSessionCache, BffLocalSessionCache>();

        if (configuration.IsRedisConfigured())
        {
            // L2: PostgreSQL primary store + Redis read-through cache (sliding TTL, tokens encrypted at rest).
            services.AddSingleton<PostgresCachedBffSessionStore>();
            // Publishes revoked session ids to the bff:session:revocations pub/sub channel so peer
            // instances can evict their L1 entries within milliseconds instead of waiting for TTL expiry.
            services.AddSingleton<
                IBffSessionRevocationNotifier,
                RedisBffSessionRevocationNotifier
            >();
            // Decorator that layers L1 over L2: cache hits skip the store entirely; terminal mutations
            // invalidate L1 and trigger a revocation broadcast.
            services.AddSingleton<IBffSessionStore>(sp => new CachingBffSessionStoreDecorator(
                sp.GetRequiredService<PostgresCachedBffSessionStore>(),
                sp.GetRequiredService<IBffLocalSessionCache>(),
                sp.GetRequiredService<IBffSessionRevocationNotifier>()
            ));
            // Distributed refresh lock: ensures only one instance exchanges the refresh token at a time,
            // preventing concurrent refresh races under load.
            services.AddSingleton<IBffRefreshCoordinator, RedisBffRefreshCoordinator>();
            // Background subscriber: listens on bff:session:revocations and calls IBffLocalSessionCache.Invalidate
            // for each received session id. Subscribed before HTTP traffic is accepted (IHostedService.StartAsync).
            services.AddHostedService<BffSessionRevocationSubscriber>();
        }
        else
        {
            // L2 fallback: PostgreSQL primary store + IDistributedCache (no Redis-specific features).
            services.AddSingleton<PostgresDistributedCacheBffSessionStore>();
            // No Redis → no pub/sub; cross-instance L1 staleness is bounded by LocalCacheTtlSeconds only.
            services.AddSingleton<
                IBffSessionRevocationNotifier,
                NullBffSessionRevocationNotifier
            >();
            services.AddSingleton<IBffSessionStore>(sp => new CachingBffSessionStoreDecorator(
                sp.GetRequiredService<PostgresDistributedCacheBffSessionStore>(),
                sp.GetRequiredService<IBffLocalSessionCache>(),
                sp.GetRequiredService<IBffSessionRevocationNotifier>()
            ));
            // In-process lock: adequate for single-instance deployments; replaced by RedisBffRefreshCoordinator
            // when Redis is present.
            services.AddSingleton<IBffRefreshCoordinator, InProcessBffRefreshCoordinator>();
        }

        // Stores ASP.NET Core authentication tickets server-side; the browser cookie carries only
        // an opaque key — no token data ever reaches the client.
        services.AddSingleton<RedisTicketStore>();
        services
            .AddOptions<CookieAuthenticationOptions>(AuthConstants.BffSchemes.Cookie)
            .Configure<RedisTicketStore>((opts, store) => opts.SessionStore = store);
    }

    /// <summary>
    ///     Registers the BFF session application-layer services.
    ///     <see cref="BffSessionService" /> is the central coordinator and is intentionally exposed
    ///     under both <see cref="IBffSessionService" /> (read/write) and
    ///     <see cref="IBffSessionRevocationService" /> (revocation) to keep consumer interfaces
    ///     focused. <see cref="IBffTokenRefreshService" /> is scoped because it participates in the
    ///     per-request cookie refresh pipeline via <see cref="CookieSessionRefresher" />.
    /// </summary>
    private static void AddBffSessionServices(IServiceCollection services)
    {
        // Enforces session business rules (expiry, revocation status) before the principal is built.
        services.AddSingleton<IBffSessionValidator, BffSessionValidator>();
        // Constructs new BffSessionRecord instances with all required fields populated and tokens encrypted.
        services.AddSingleton<IBffSessionRecordFactory, BffSessionRecordFactory>();
        // Applies state transitions (refresh, revoke, expire) to an existing record using optimistic concurrency.
        services.AddSingleton<IBffSessionMutator, BffSessionMutator>();
        // Central coordinator: registered as a concrete type so it can be resolved under two interfaces below.
        services.AddSingleton<BffSessionService>();
        // Exposed for read/write session operations (controllers, middleware).
        services.AddSingleton<IBffSessionService>(sp => sp.GetRequiredService<BffSessionService>());
        // Exposed for revocation operations (logout, password-change handlers); intentionally narrow interface.
        services.AddSingleton<IBffSessionRevocationService>(sp =>
            sp.GetRequiredService<BffSessionService>()
        );
        // Scoped because it is called inside CookieSessionRefresher which runs within the request pipeline.
        services.AddScoped<IBffTokenRefreshService, BffTokenRefreshService>();
    }

    /// <summary>
    ///     Configures JWT Bearer post-processing hooks, claims transformation, and authorization policies.
    ///     <para>
    ///         The JWT Bearer post-configure hook wires <see cref="IdentityTokenValidatedPipeline" />
    ///         so both the JWT Bearer and Cookie schemes run the same tenant-validation logic after a
    ///         token is validated.
    ///     </para>
    ///     <para>
    ///         <see cref="UserPermissionsClaimsTransformation" /> enriches the principal with
    ///         application-level permission claims derived from Keycloak roles.
    ///     </para>
    ///     <para>
    ///         The fallback policy requires an authenticated user on either scheme. The
    ///         <c>PlatformAdmin</c> policy requires the <c>Platform.Manage</c> permission claim; the
    ///         <c>TenantAdmin</c> policy accepts either <c>Tenant.Manage</c> or <c>Platform.Manage</c>
    ///         because platform admins subsume tenant admin rights.
    ///     </para>
    /// </summary>
    private static void AddBffAuthorizationPolicies(IServiceCollection services)
    {
        // Attaches tenant-validation and permission-claim extraction to the JWT Bearer scheme so
        // both JWT and Cookie callers go through the same IdentityTokenValidatedPipeline.
        services.PostConfigure<JwtBearerOptions>(
            JwtBearerDefaults.AuthenticationScheme,
            options =>
            {
                options.Events ??= new JwtBearerEvents();
                options.Events.OnTokenValidated = IdentityTokenValidatedPipeline.OnTokenValidated;
                // Returns 403 with a structured ProblemDetails body instead of the default 401 redirect.
                options.Events.OnChallenge = JwtBearerAccessDeniedChallenge.OnChallengeAsync;
            }
        );

        // Translates Keycloak realm roles into application-level permission claims on every request.
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
                            SharedKernel.Contracts.Security.Permission.Platform.Manage
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
                            SharedKernel.Contracts.Security.Permission.Tenant.Manage,
                            SharedKernel.Contracts.Security.Permission.Platform.Manage
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

    // ── Application Services ─────────────────────────────────────────────────

    private static void RegisterApplicationServices(IServiceCollection services)
    {
        services.AddScoped<ISecureTokenGenerator, SecureTokenGenerator>();
        services.AddSingleton<IKeycloakService, KeycloakService>();
        services.AddSingleton<IExternalIdentityProvider, GoogleIdentityProvider>();
        services.AddScoped<IUserUniquenessChecker, UserUniquenessChecker>();
        services.AddScoped<ITenantUniquenessChecker, TenantUniquenessChecker>();
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
