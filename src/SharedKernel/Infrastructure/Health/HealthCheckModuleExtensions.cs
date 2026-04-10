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

        ServiceCollection tempServices = new();
        tempServices.AddSingleton(configuration);
        tempServices.AddSingleton(environment);
        ServiceProvider tempProvider = tempServices.BuildServiceProvider();

        foreach (Type type in moduleTypes)
        {
            IHealthCheckModule module = (IHealthCheckModule)
                ActivatorUtilities.CreateInstance(tempProvider, type);
            module.RegisterHealthChecks(builder);
        }
        return services;
    }
}
