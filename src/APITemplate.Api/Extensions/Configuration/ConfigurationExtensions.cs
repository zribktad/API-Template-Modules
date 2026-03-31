namespace APITemplate.Api.Extensions.Configuration;

internal static class ConfigurationExtensions
{
    public static IConfigurationSection SectionFor<TOptions>(this IConfiguration configuration)
        where TOptions : class
        => SharedKernel.Infrastructure.Configuration.ConfigurationExtensions.SectionFor<TOptions>(
            configuration
        );
}
