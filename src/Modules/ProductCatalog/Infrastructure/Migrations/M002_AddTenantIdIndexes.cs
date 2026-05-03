using Kot.MongoDB.Migrations;
using MongoDB.Bson;
using MongoDB.Driver;

namespace ProductCatalog.Infrastructure.Migrations;

/// <summary>
///     Adds indexes to the product_data collection to prevent full-collection scans
///     when querying by TenantId and Id, or TenantId and IsDeleted.
///     (Note: ProductId is managed in PostgreSQL via ProductDataLink, so we index on _id instead).
/// </summary>
public sealed class M002_AddTenantIdIndexes : MongoMigration
{
    public M002_AddTenantIdIndexes()
        : base("1.0.1") { }

    public override async Task UpAsync(
        IMongoDatabase db,
        IClientSessionHandle session,
        CancellationToken ct
    )
    {
        IMongoCollection<BsonDocument> collection = db.GetCollection<BsonDocument>("product_data");

        // Index: { TenantId: 1, _id: 1 }
        var tenantIdIdIndex = new CreateIndexModel<BsonDocument>(
            Builders<BsonDocument>.IndexKeys.Ascending("TenantId").Ascending("_id"),
            new CreateIndexOptions { Name = "idx_TenantId_Id" }
        );

        // Index: { TenantId: 1, IsDeleted: 1 }
        var tenantIdIsDeletedIndex = new CreateIndexModel<BsonDocument>(
            Builders<BsonDocument>.IndexKeys.Ascending("TenantId").Ascending("IsDeleted"),
            new CreateIndexOptions { Name = "idx_TenantId_IsDeleted" }
        );

        await collection.Indexes.CreateManyAsync(
            session,
            new[] { tenantIdIdIndex, tenantIdIsDeletedIndex },
            cancellationToken: ct
        );
    }

    public override async Task DownAsync(
        IMongoDatabase db,
        IClientSessionHandle session,
        CancellationToken ct
    )
    {
        IMongoCollection<BsonDocument> collection = db.GetCollection<BsonDocument>("product_data");

        await collection.Indexes.DropOneAsync(session, "idx_TenantId_Id", cancellationToken: ct);
        await collection.Indexes.DropOneAsync(
            session,
            "idx_TenantId_IsDeleted",
            cancellationToken: ct
        );
    }
}
