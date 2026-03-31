namespace APITemplate.Api.Extensions.Configuration;

/// <summary>
/// Configuration keys that cannot be derived from an Options class name by convention.
/// All other section names are resolved automatically via
/// <see cref="ConfigurationExtensions.SectionFor{TOptions}"/>.
/// </summary>
internal static class ConfigurationSections
{
    public const string DefaultConnection =
        SharedKernel.Infrastructure.Configuration.ConfigurationSections.DefaultConnection;
    public const string MongoDB =
        SharedKernel.Infrastructure.Configuration.ConfigurationSections.MongoDB;
}
