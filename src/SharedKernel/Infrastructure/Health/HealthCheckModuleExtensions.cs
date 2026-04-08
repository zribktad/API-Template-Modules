using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace SharedKernel.Infrastructure.Health;

public static class HealthCheckModuleExtensions
{
    /// <summary>
    ///     Registers health checks by instantiating each <see cref="IHealthCheckModule" /> type from
    ///     <paramref name="moduleTypes" /> using <see cref="Activator.CreateInstance" /> with
    ///     <paramref name="configuration" /> as the sole constructor argument.
    /// </summary>
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
