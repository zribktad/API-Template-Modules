using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using BuildingBlocks.Application.Options.Infrastructure;
using BuildingBlocks.Application.Configuration;

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

        ServiceCollection tempServices = new();
        tempServices.AddSingleton(configuration);
        tempServices.AddSingleton(environment);
        using ServiceProvider tempProvider = tempServices.BuildServiceProvider();

        foreach (Type type in moduleTypes)
        {
            IHealthCheckModule module = (IHealthCheckModule)
                ActivatorUtilities.CreateInstance(tempProvider, type);
            module.RegisterHealthChecks(builder);
        }
        return services;
    }
}

