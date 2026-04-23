using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Serializers;
using MongoDB.Driver;
using MongoDB.Driver.Core.Extensions.DiagnosticSources;

namespace ProductCatalog.Persistence;

/// <summary>
///     Thin wrapper around the MongoDB driver that configures the client with diagnostic
///     activity tracing and exposes typed collection accessors for domain document types.
/// </summary>
public sealed class MongoDbContext
{
    private readonly IMongoDatabase _database;

    static MongoDbContext()
    {
        BsonSerializer.TryRegisterSerializer(new GuidSerializer(GuidRepresentation.Standard));
    }

    public MongoDbContext(IOptions<MongoDbSettings> settings)
    {
        MongoClientSettings? clientSettings = MongoClientSettings.FromConnectionString(
            settings.Value.ConnectionString
        );
        clientSettings.ServerSelectionTimeout = TimeSpan.FromSeconds(5);
        clientSettings.ClusterConfigurator = cb =>
            cb.Subscribe(new DiagnosticsActivityEventSubscriber());
        MongoClient client = new(clientSettings);
        _database = client.GetDatabase(settings.Value.DatabaseName);
    }

    public IMongoCollection<ProductData> ProductData =>
        _database.GetCollection<ProductData>("product_data");

    /// <summary>Sends a ping command to verify that the MongoDB server is reachable.</summary>
    public Task PingAsync(CancellationToken cancellationToken = default)
    {
        return _database.RunCommandAsync<BsonDocument>(
            new BsonDocument("ping", 1),
            cancellationToken: cancellationToken
        );
    }
}
