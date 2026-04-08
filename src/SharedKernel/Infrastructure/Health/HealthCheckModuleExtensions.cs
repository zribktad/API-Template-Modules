using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace SharedKernel.Infrastructure.Health;

public static class HealthCheckModuleExtensions
{
    public static IServiceCollection AddModuleHealthChecks(
        this IServiceCollection services,
        IConfiguration configuration,
        IReadOnlyList<Type> moduleTypes
    )
    {
        IHealthChecksBuilder builder = services.AddHealthChecks();
        foreach (Type type in moduleTypes)
        {
            IHealthCheckModule module = (IHealthCheckModule)
                Activator.CreateInstance(type, configuration)!;
            module.RegisterHealthChecks(builder);
        }
        return services;
    }
}
