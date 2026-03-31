using Microsoft.Extensions.Options;

namespace APITemplate.Api.Extensions.Configuration;

internal static class ServiceCollectionOptionsExtensions
{
    public static OptionsBuilder<TOptions> AddValidatedOptions<TOptions>(
        this IServiceCollection services,
        IConfiguration configuration,
        bool validateDataAnnotations = true
    )
        where TOptions : class
        => SharedKernel.Infrastructure.Configuration.ServiceCollectionOptionsExtensions.AddValidatedOptions<TOptions>(
            services,
            configuration,
            validateDataAnnotations
        );
}
