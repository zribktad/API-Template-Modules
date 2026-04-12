using FluentValidation;
using Identity.Directory.Controllers.V1;
using Identity.Directory.Repositories;
using Identity.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SharedKernel.Infrastructure.Configuration;
using SharedKernel.Infrastructure.Registration;

namespace Identity;

public static partial class IdentityModule
{
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
            .AddRepository<ITenantInvitationRepository, TenantInvitationRepository>()
            .AddRepository<IRoleRepository, RoleRepository>();

        services.AddScoped<AuthBootstrapSeeder>();
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
