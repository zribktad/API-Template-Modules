using System.Reflection;
using BuildingBlocks.Application.Configuration;
using BuildingBlocks.Application.Options.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;

namespace BuildingBlocks.Web.Health;

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
        RuntimeFeaturesOptions runtimeFeatures =
            configuration.SectionFor<RuntimeFeaturesOptions>().Get<RuntimeFeaturesOptions>()
            ?? new RuntimeFeaturesOptions();
        if (!runtimeFeatures.ModuleHealthChecksEnabled)
        {
            services.AddOptions<HealthCheckServiceOptions>();
            return services;
        }

        foreach (Type type in moduleTypes)
        {
            IHealthCheckModule module = CreateHealthCheckModule(type, configuration, environment);
            module.RegisterHealthChecks(builder);
        }

        return services;
    }

    private static IHealthCheckModule CreateHealthCheckModule(
        Type type,
        IConfiguration configuration,
        IHostEnvironment environment
    )
    {
        // Manual instantiation to avoid BuildServiceProvider anti-pattern during startup.
        ConstructorInfo[] constructors = type.GetConstructors();
        foreach (ConstructorInfo ctor in constructors)
        {
            ParameterInfo[] parameters = ctor.GetParameters();
            object?[] args = new object?[parameters.Length];
            bool possible = true;

            for (int i = 0; i < parameters.Length; i++)
            {
                if (parameters[i].ParameterType == typeof(IConfiguration))
                {
                    args[i] = configuration;
                }
                else if (parameters[i].ParameterType == typeof(IHostEnvironment))
                {
                    args[i] = environment;
                }
                else
                {
                    possible = false;
                    break;
                }
            }

            if (possible)
            {
                return (IHealthCheckModule)ctor.Invoke(args);
            }
        }

        throw new InvalidOperationException(
            $"No suitable constructor found for health check module {type.FullName}. "
                + "Supported parameters: IConfiguration, IHostEnvironment."
        );
    }
}
