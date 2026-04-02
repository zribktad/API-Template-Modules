using Microsoft.Extensions.Options;

namespace APITemplate.Api.Extensions.Configuration;

internal static class ServiceCollectionOptionsExtensions
{
    /// <summary>
    /// Binds <typeparamref name="TOptions"/> to its configuration section (via
    /// <see cref="ConfigurationExtensions.SectionFor{TOptions}"/>), optionally validates
    /// data annotations, and validates eagerly on application start.
    /// </summary>
    public static OptionsBuilder<TOptions> AddValidatedOptions<TOptions>(
        this IServiceCollection services,
        IConfiguration configuration,
        bool validateDataAnnotations = true
    )
        where TOptions : class
    {
        var builder = services.AddOptions<TOptions>().Bind(configuration.SectionFor<TOptions>());
        if (validateDataAnnotations)
            builder.ValidateDataAnnotations();
        builder.ValidateOnStart();
        return builder;
    }
}
