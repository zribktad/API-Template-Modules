using Identity.Auth.Options;
using Identity.Auth.Validation;
using Identity.Configuration;
using Identity.Directory.Options;
using Identity.Options;
using Identity.Persistence;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.OutputCaching;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using SharedKernel.Infrastructure.Configuration;
using SharedKernel.Infrastructure.Startup;

namespace Identity;

public static partial class IdentityModule
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
        services.AddSingleton<IValidateOptions<BffOptions>, BffOptionsValidator>();
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
}
