namespace APITemplate.Api.Extensions.Configuration;

internal static class ConfigurationExtensions
{
    private const string OptionsSuffix = "Options";

    /// <summary>
    /// Returns the configuration section whose key is derived from <typeparamref name="TOptions"/>
    /// by stripping the trailing "Options" suffix (e.g. <c>EmailOptions</c> → <c>"Email"</c>).
    /// </summary>
    public static IConfigurationSection SectionFor<TOptions>(this IConfiguration configuration)
        where TOptions : class
    {
        var name = typeof(TOptions).Name;
        var sectionName = name.EndsWith(OptionsSuffix, StringComparison.Ordinal)
            ? name[..^OptionsSuffix.Length]
            : name;
        return configuration.GetSection(sectionName);
    }
}
