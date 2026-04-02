namespace APITemplate.Infrastructure.Persistence;

/// <summary>
/// Strongly-typed settings for the MongoDB connection, bound from the application configuration.
/// </summary>
public sealed class MongoDbSettings
{
    public string ConnectionString { get; init; } = string.Empty;

    public string DatabaseName { get; init; } = string.Empty;
}
