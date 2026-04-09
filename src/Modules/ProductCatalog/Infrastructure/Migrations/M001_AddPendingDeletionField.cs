using Kot.MongoDB.Migrations;
using MongoDB.Bson;
using MongoDB.Driver;

namespace ProductCatalog.Infrastructure.Migrations;

/// <summary>
///     Backfills existing ProductData documents with <c>PendingDeletion = false</c>
///     so the mark-and-sweep orphan cleanup can query on this field consistently.
/// </summary>
public sealed class M001_AddPendingDeletionField : MongoMigration
{
    public M001_AddPendingDeletionField()
        : base("1.0.0") { }

    public override async Task UpAsync(
        IMongoDatabase db,
        IClientSessionHandle session,
        CancellationToken ct
    )
    {
        IMongoCollection<BsonDocument> collection = db.GetCollection<BsonDocument>("product_data");

        FilterDefinition<BsonDocument> filter = Builders<BsonDocument>.Filter.Not(
            Builders<BsonDocument>.Filter.Exists("PendingDeletion")
        );

        UpdateDefinition<BsonDocument> update = Builders<BsonDocument>.Update.Set(
            "PendingDeletion",
            false
        );

        await collection.UpdateManyAsync(session, filter, update, cancellationToken: ct);
    }

    public override async Task DownAsync(
        IMongoDatabase db,
        IClientSessionHandle session,
        CancellationToken ct
    )
    {
        IMongoCollection<BsonDocument> collection = db.GetCollection<BsonDocument>("product_data");

        UpdateDefinition<BsonDocument> update = Builders<BsonDocument>.Update.Unset(
            "PendingDeletion"
        );

        await collection.UpdateManyAsync(
            session,
            FilterDefinition<BsonDocument>.Empty,
            update,
            cancellationToken: ct
        );
    }
}
