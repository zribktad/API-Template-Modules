using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace BuildingBlocks.Application.Options;

public static class OptionsExtensions
{
    public static IServiceCollection AddModuleOptions<TOptions>(
        this IServiceCollection services,
        IConfiguration configuration
    )
        where TOptions : class, IModuleOptions
    {
        services
            .AddOptions<TOptions>()
            .Bind(configuration.GetSection(TOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        return services;
    }
}
