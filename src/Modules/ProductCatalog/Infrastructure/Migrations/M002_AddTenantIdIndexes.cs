using Kot.MongoDB.Migrations;
using MongoDB.Bson;
using MongoDB.Driver;
using ProductCatalog.Entities.ProductData;

namespace ProductCatalog.Infrastructure.Migrations;

/// <summary>
///     Adds indexes to the product_data collection to prevent full-collection scans
///     when querying by TenantId and IsDeleted.
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
        IMongoCollection<BsonDocument> collection = db.GetCollection<BsonDocument>(
            ProductData.CollectionName
        );

        // Index: { tenantId: 1, isDeleted: 1 }
        var tenantIdIsDeletedIndex = new CreateIndexModel<BsonDocument>(
            Builders<BsonDocument>.IndexKeys.Ascending("tenantId").Ascending("isDeleted"),
            new CreateIndexOptions { Name = "idx_tenantId_isDeleted" }
        );

        await collection.Indexes.CreateOneAsync(
            session,
            tenantIdIsDeletedIndex,
            cancellationToken: ct
        );
    }

    public override async Task DownAsync(
        IMongoDatabase db,
        IClientSessionHandle session,
        CancellationToken ct
    )
    {
        IMongoCollection<BsonDocument> collection = db.GetCollection<BsonDocument>(
            ProductData.CollectionName
        );

        await collection.Indexes.DropOneAsync(
            session,
            "idx_tenantId_isDeleted",
            cancellationToken: ct
        );
    }
}
