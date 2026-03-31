using APITemplate.Domain.Entities;
using Kot.MongoDB.Migrations;
using MongoDB.Driver;

namespace APITemplate.Infrastructure.Migrations;

public sealed class M002_AddProductDataSoftDeleteIndexes : MongoMigration
{
    public M002_AddProductDataSoftDeleteIndexes() : base("1.1.0") { }

    public override Task UpAsync(IMongoDatabase db, IClientSessionHandle session, CancellationToken ct)
    {
        var collection = db.GetCollection<ProductData>("product_data");

        return collection.Indexes.CreateManyAsync(
        [
            new CreateIndexModel<ProductData>(
                Builders<ProductData>.IndexKeys.Ascending(x => x.TenantId).Ascending(x => x.IsDeleted).Ascending("_t"),
                new CreateIndexOptions { Name = "idx_tenant_is_deleted_type" }),
            new CreateIndexModel<ProductData>(
                Builders<ProductData>.IndexKeys.Ascending(x => x.TenantId).Ascending(x => x.IsDeleted).Descending(x => x.CreatedAt),
                new CreateIndexOptions { Name = "idx_tenant_is_deleted_created" }),
            new CreateIndexModel<ProductData>(
                Builders<ProductData>.IndexKeys.Ascending(x => x.TenantId).Ascending(x => x.Id).Ascending(x => x.IsDeleted),
                new CreateIndexOptions { Name = "idx_tenant_id_is_deleted" })
        ], ct);
    }

    public override async Task DownAsync(IMongoDatabase db, IClientSessionHandle session, CancellationToken ct)
    {
        var collection = db.GetCollection<ProductData>("product_data");
        await collection.Indexes.DropOneAsync("idx_tenant_is_deleted_type", ct);
        await collection.Indexes.DropOneAsync("idx_tenant_is_deleted_created", ct);
        await collection.Indexes.DropOneAsync("idx_tenant_id_is_deleted", ct);
    }
}
