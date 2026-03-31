using APITemplate.Domain.Entities;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Driver;
using MongoDB.Driver.Core.Extensions.DiagnosticSources;

namespace APITemplate.Infrastructure.Persistence;

/// <summary>
/// Thin wrapper around the MongoDB driver that configures the client with diagnostic
/// activity tracing and exposes typed collection accessors for domain document types.
/// </summary>
public sealed class MongoDbContext
{
    private readonly IMongoDatabase _database;

    public MongoDbContext(IOptions<MongoDbSettings> settings)
    {
        var clientSettings = MongoClientSettings.FromConnectionString(
            settings.Value.ConnectionString
        );
        clientSettings.ServerSelectionTimeout = TimeSpan.FromSeconds(5);
        clientSettings.ClusterConfigurator = cb =>
            cb.Subscribe(new DiagnosticsActivityEventSubscriber());
        var client = new MongoClient(clientSettings);
        _database = client.GetDatabase(settings.Value.DatabaseName);
    }

    public IMongoCollection<ProductData> ProductData =>
        _database.GetCollection<ProductData>("product_data");

    /// <summary>Sends a ping command to verify that the MongoDB server is reachable.</summary>
    public Task PingAsync(CancellationToken cancellationToken = default) =>
        _database.RunCommandAsync<BsonDocument>(
            new BsonDocument("ping", 1),
            cancellationToken: cancellationToken
        );
}
