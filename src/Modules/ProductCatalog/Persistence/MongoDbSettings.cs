using System.ComponentModel.DataAnnotations;
using BuildingBlocks.Application.Options;

namespace ProductCatalog.Persistence;

/// <summary>
///     Strongly-typed settings for the MongoDB connection, bound from the application configuration.
/// </summary>
public sealed class MongoDbSettings : IModuleOptions
{
    public static string SectionName => "MongoDB";

    [Required]
    public string ConnectionString { get; init; } = string.Empty;

    [Required]
    public string DatabaseName { get; init; } = string.Empty;
}
