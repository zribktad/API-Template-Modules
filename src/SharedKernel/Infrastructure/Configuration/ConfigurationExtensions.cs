using Microsoft.Extensions.Configuration;

namespace SharedKernel.Infrastructure.Configuration;

/// <summary>
/// Shared configuration helpers that derive section names from options types by convention.
/// </summary>
public static class ConfigurationExtensions
{
    private const string OptionsSuffix = "Options";

    /// <summary>
    /// Returns the configuration section whose key is derived from <typeparamref name="TOptions"/>
    /// by stripping the trailing "Options" suffix.
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
