namespace APITemplate.Api.Extensions.Configuration;

/// <summary>
/// Configuration keys that cannot be derived from an Options class name by convention.
/// All other section names are resolved automatically via
/// <see cref="ConfigurationExtensions.SectionFor{TOptions}"/>.
/// </summary>
internal static class ConfigurationSections
{
    /// <summary>GetConnectionString key for the primary PostgreSQL connection.</summary>
    public const string DefaultConnection = "DefaultConnection";

    /// <summary>MongoDbSettings does not carry the "Options" suffix.</summary>
    public const string MongoDB = "MongoDB";
}
