using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace SharedKernel.Infrastructure.Configuration;

/// <summary>
/// Shared options binding helpers used across host and modules.
/// </summary>
public static class ServiceCollectionOptionsExtensions
{
    /// <summary>
    /// Binds <typeparamref name="TOptions"/> to its convention-based configuration section,
    /// optionally validates data annotations, and validates eagerly on application start.
    /// </summary>
    public static OptionsBuilder<TOptions> AddValidatedOptions<TOptions>(
        this IServiceCollection services,
        IConfiguration configuration,
        bool validateDataAnnotations = true
    )
        where TOptions : class
    {
        OptionsBuilder<TOptions> builder = services
            .AddOptions<TOptions>()
            .Bind(configuration.SectionFor<TOptions>());
        if (validateDataAnnotations)
            builder.ValidateDataAnnotations();
        builder.ValidateOnStart();
        return builder;
    }
}
