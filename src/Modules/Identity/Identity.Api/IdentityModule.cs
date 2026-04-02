using FluentValidation;
using Identity.Api.Authorization;
using Identity.Api.Controllers.V1;
using Identity.Api.Security;
using Identity.Application.Common.Email;
using Identity.Application.Common.Security;
using Identity.Application.Features.User.Validation;
using Identity.Application.Options;
using Identity.Domain.Enums;
using Identity.Domain.Interfaces;
using Identity.Infrastructure.Persistence;
using Identity.Infrastructure.Persistence.EntityNormalization;
using Identity.Infrastructure.Repositories;
using Identity.Infrastructure.Security;
using Identity.Infrastructure.Security.Keycloak;
using Identity.Infrastructure.Security.Tenant;
using Identity.Infrastructure.SoftDelete;
using Keycloak.AuthServices.Sdk;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using Polly;
using SharedKernel.Application.Options;
using SharedKernel.Application.Resilience;
using SharedKernel.Domain.Entities;
using SharedKernel.Infrastructure.Configuration;
using SharedKernel.Infrastructure.EntityNormalization;
using SharedKernel.Infrastructure.Registration;
using SharedKernel.Infrastructure.Resilience;

namespace Identity.Api;

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

        return services;
    }

    public static IEndpointRouteBuilder MapIdentityEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapControllers();
        return endpoints;
    }

    // ── Options ──────────────────────────────────────────────────────────────

    private static void RegisterOptions(IServiceCollection services, IConfiguration configuration)
    {
        services.AddValidatedOptions<KeycloakOptions>(configuration);
        services.AddValidatedOptions<BffOptions>(configuration, validateDataAnnotations: false);
        services.AddValidatedOptions<CorsOptions>(configuration, validateDataAnnotations: false);
        services
            .AddValidatedOptions<BootstrapTenantOptions>(configuration)
            .Validate(
                o => !string.IsNullOrWhiteSpace(o.Code) && !string.IsNullOrWhiteSpace(o.Name),
                "Bootstrap tenant code/name is required"
            );
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

        // Augment existing authentication with BFF schemes.
        services
            .AddAuthentication()
            .AddCookie(AuthConstants.BffSchemes.Cookie, options => ConfigureCookie(options, bff))
            .AddOpenIdConnect(
                AuthConstants.BffSchemes.Oidc,
                options => ConfigureOidc(options, keycloak, bff, authority)
            );

        // Override JWT bearer events to enable tenant claim validation + user provisioning.
        services.PostConfigure<JwtBearerOptions>(
            JwtBearerDefaults.AuthenticationScheme,
            options =>
            {
                options.Events ??= new JwtBearerEvents();
                options.Events.OnTokenValidated = TenantClaimValidator.OnTokenValidated;
            }
        );

        // Distributed ticket store (DragonFly/Redis) keeps the cookie payload small.
        services.AddSingleton<DragonflyTicketStore>();
        services
            .AddOptions<CookieAuthenticationOptions>(AuthConstants.BffSchemes.Cookie)
            .Configure<DragonflyTicketStore>((opts, store) => opts.SessionStore = store);

        // Fallback policy: require authenticated user via JWT or BFF cookie.
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

        services.AddHttpClient(AuthConstants.HttpClients.KeycloakToken);
    }

    private static void ConfigureCookie(CookieAuthenticationOptions options, BffOptions bff)
    {
        options.Cookie.Name = bff.CookieName;
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.ExpireTimeSpan = TimeSpan.FromMinutes(bff.SessionTimeoutMinutes);
        options.SlidingExpiration = true;
        options.Events.OnRedirectToLogin = context =>
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return Task.CompletedTask;
        };
        options.Events.OnValidatePrincipal = CookieSessionRefresher.OnValidatePrincipal;
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
            OnTokenValidated = TenantClaimValidator.OnTokenValidated,
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

        // Register entity normalization before AddDefaultInfrastructure so TryAdd doesn't override it.
        services.AddSingleton<IEntityNormalizationService, AppUserEntityNormalizationService>();

        services
            .AddModule<IdentityDbContext>(configuration)
            .ConfigureDbContext(opts => opts.UseNpgsql(connectionString))
            .AddDefaultInfrastructure()
            .ForwardUnitOfWork<Identity.Domain.IdentityDbMarker>()
            .AddRepository<IUserRepository, UserRepository>()
            .AddRepository<ITenantRepository, TenantRepository>()
            .AddRepository<ITenantInvitationRepository, TenantInvitationRepository>()
            .AddCascadeRule<TenantSoftDeleteCascadeRule>();

        services.AddScoped<AuthBootstrapSeeder>();
    }

    // ── Application Services ─────────────────────────────────────────────────

    private static void RegisterApplicationServices(IServiceCollection services)
    {
        services.AddScoped<IUserProvisioningService, UserProvisioningService>();
        services.AddScoped<ISecureTokenGenerator, SecureTokenGenerator>();
        services.AddSingleton<ITenantCodeConflictDetector, PostgresTenantCodeConflictDetector>();
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
