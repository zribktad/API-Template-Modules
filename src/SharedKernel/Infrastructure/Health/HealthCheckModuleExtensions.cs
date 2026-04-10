using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace SharedKernel.Infrastructure.Health;

public static class HealthCheckModuleExtensions
{
    public static IServiceCollection AddModuleHealthChecks(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment,
        IReadOnlyList<Type> moduleTypes
    )
    {
        IHealthChecksBuilder builder = services.AddHealthChecks();
        object[] args = [configuration, environment];

        foreach (Type type in moduleTypes)
        {
            IHealthCheckModule module = (IHealthCheckModule)
                Activator.CreateInstance(type, args)!;
            module.RegisterHealthChecks(builder);
        }
        return services;
    }
}
