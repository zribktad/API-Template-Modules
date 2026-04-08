using Microsoft.Extensions.DependencyInjection;

namespace SharedKernel.Infrastructure.Health;

public static class HealthCheckModuleExtensions
{
    public static IServiceCollection AddModuleHealthChecks(
        this IServiceCollection services,
        params IHealthCheckModule[] modules
    )
    {
        IHealthChecksBuilder builder = services.AddHealthChecks();
        foreach (IHealthCheckModule module in modules)
            module.RegisterHealthChecks(builder);
        return services;
    }
}
