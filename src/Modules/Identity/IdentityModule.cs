using FluentValidation;
using Identity.Common.Email;
using Identity.Configuration;
using Identity.Controllers.V1;
using Identity.Options;
using Identity.Persistence;
using Identity.Repositories;
using Identity.Security;
using Identity.Security.ExternalIdentityProviders;
using Identity.Security.Keycloak;
using Identity.Security.Sessions;
using Identity.Security.Tenant;
using Keycloak.AuthServices.Sdk;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.OutputCaching;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Polly;
using SharedKernel.Application.Resilience;
using SharedKernel.Infrastructure.Configuration;
using SharedKernel.Infrastructure.Registration;
using SharedKernel.Infrastructure.Resilience;
using SharedKernel.Infrastructure.Startup;

namespace Identity;

public static class IdentityModule
{
    public static IServiceCollection AddIdentityModule(
        this IServiceCollection services,
        IConfiguration configuration
    )
    {
        RegisterOptions(services, configuration);
        RegisterCors(services, configuration);
        RegisterBffAuthentication(services, configuration);
        RegisterDbInfrastructure(services, configuration);
        RegisterApplicationServices(services);
        RegisterKeycloakAdmin(services);
        RegisterValidators(services);
        RegisterControllers(services);

        services.AddSingleton<IDatabaseStartupContributor, IdentityDatabaseStartupContributor>();
        services.AddSingleton<
            IConfigureOptions<OutputCacheOptions>,
            IdentityOutputCacheOptionsSetup
        >();

        return services;
    }

    public static IEndpointRouteBuilder MapIdentityEndpoints(this IEndpointRouteBuilder endpoints)
    {
        return endpoints;
    }

    // ── Options ──────────────────────────────────────────────────────────────

    private static void RegisterOptions(IServiceCollection services, IConfiguration configuration)
    {
        services.AddValidatedOptions<KeycloakOptions>(configuration);
        services.AddValidatedOptions<BffOptions>(configuration);
        services.AddValidatedOptions<CorsOptions>(configuration);
        services
            .AddValidatedOptions<BootstrapTenantOptions>(configuration)
            .Validate(
                o => !string.IsNullOrWhiteSpace(o.Code) && !string.IsNullOrWhiteSpace(o.Name),
                "Bootstrap tenant code/name is required"
            );
        services.AddValidatedOptions<TenantInvitationOptions>(configuration);
        services.AddValidatedOptions<SystemIdentityOptions>(configuration);
    }

    // ── CORS ─────────────────────────────────────────────────────────────────

    private static void RegisterCors(IServiceCollection services, IConfiguration configuration)
    {
        string[] corsOrigins = (
            configuration.SectionFor<CorsOptions>().Get<CorsOptions>() ?? new CorsOptions()
        )
            .AllowedOrigins.Where(o => !string.IsNullOrWhiteSpace(o))
            .Select(o => o.Trim())
            .ToArray();

        if (corsOrigins.Length > 0)
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
    }

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
        services.AddSingleton<IBffSessionStore, PostgresCachedBffSessionStore>();
        services.AddSingleton<BffSessionService>();
        services.AddSingleton<IBffSessionService>(sp => sp.GetRequiredService<BffSessionService>());
        services.AddSingleton<IBffSessionRevocationService>(sp =>
            sp.GetRequiredService<BffSessionService>()
        );
        services.AddSingleton<IBffRefreshCoordinator, DragonflyBffRefreshCoordinator>();
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

        services.AddSingleton<DragonflyTicketStore>();
        services
            .AddOptions<CookieAuthenticationOptions>(AuthConstants.BffSchemes.Cookie)
            .Configure<DragonflyTicketStore>((opts, store) => opts.SessionStore = store);

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

        services
            .AddHttpClient(
                AuthConstants.HttpClients.KeycloakToken,
                client => client.Timeout = TimeSpan.FromSeconds(10)
            )
            .AddResilienceHandler(
                ResiliencePipelineKeys.KeycloakToken,
                builder =>
                {
                    builder.AddRetry(
                        new HttpRetryStrategyOptions
                        {
                            MaxRetryAttempts = ResilienceDefaults.MaxRetryAttempts,
                            BackoffType = DelayBackoffType.Exponential,
                            Delay = ResilienceDefaults.ShortDelay,
                            UseJitter = true,
                        }
                    );
                }
            );
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
        options.RequireHttpsMetadata = !authority.StartsWith(
            "http://",
            StringComparison.OrdinalIgnoreCase
        );
        options.ClientId = keycloak.Resource;
        options.ClientSecret = keycloak.Credentials.Secret;
        options.ResponseType = OpenIdConnectResponseType.Code;
        options.SaveTokens = true;
        options.SignInScheme = AuthConstants.BffSchemes.Cookie;

        options.Scope.Clear();
        foreach (string scope in bff.Scopes)
            options.Scope.Add(scope);

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

    // ── Database + Repositories ───────────────────────────────────────────────

    private static void RegisterDbInfrastructure(
        IServiceCollection services,
        IConfiguration configuration
    )
    {
        string connectionString = configuration.GetConnectionString(
            ConfigurationSections.DefaultConnection
        )!;

        services
            .AddModule<IdentityDbContext>(configuration)
            .ConfigureDbContext(opts => opts.UseNpgsql(connectionString))
            .AddDefaultInfrastructure()
            .ForwardUnitOfWork<IdentityDbMarker>()
            .AddRepository<IUserRepository, UserRepository>()
            .AddRepository<ITenantRepository, TenantRepository>()
            .AddRepository<ITenantInvitationRepository, TenantInvitationRepository>();

        services.AddScoped<AuthBootstrapSeeder>();
    }

    // ── Application Services ─────────────────────────────────────────────────

    private static void RegisterApplicationServices(IServiceCollection services)
    {
        services.AddScoped<ISecureTokenGenerator, SecureTokenGenerator>();
        services.AddSingleton<IKeycloakService, KeycloakService>();
        services.AddSingleton<ITenantCodeConflictDetector, PostgresTenantCodeConflictDetector>();
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
            .AddResilienceHandler(
                ResiliencePipelineKeys.KeycloakAdmin,
                builder =>
                {
                    builder.AddRetry(
                        new HttpRetryStrategyOptions
                        {
                            MaxRetryAttempts = ResilienceDefaults.MaxRetryAttempts,
                            BackoffType = DelayBackoffType.Exponential,
                            Delay = ResilienceDefaults.ShortDelay,
                            UseJitter = true,
                        }
                    );
                }
            );

        services.AddScoped<IKeycloakAdminService, KeycloakAdminService>();
    }

    // ── Validators ────────────────────────────────────────────────────────────

    private static void RegisterValidators(IServiceCollection services)
    {
        services.AddValidatorsFromAssemblyContaining<CreateUserRequestValidator>(filter: r =>
            !r.ValidatorType.IsGenericTypeDefinition
        );
    }

    // ── Controllers ───────────────────────────────────────────────────────────

    private static void RegisterControllers(IServiceCollection services)
    {
        services.AddControllers().AddApplicationPart(typeof(UsersController).Assembly);
    }
}
