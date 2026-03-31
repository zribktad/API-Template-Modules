using APITemplate.Domain.Entities;
using Kot.MongoDB.Migrations;
using MongoDB.Driver;

namespace APITemplate.Infrastructure.Migrations;

/// <summary>
/// Creates indexes on the product_data collection:
///   - idx_type    : ascending on _t (discriminator) — speeds up ?type=image|video filter
///   - idx_created : descending on CreatedAt — speeds up time-based ordering
/// </summary>
public sealed class M001_CreateProductDataIndexes : MongoMigration
{
    public M001_CreateProductDataIndexes() : base("1.0.0") { }

    public override async Task UpAsync(IMongoDatabase db, IClientSessionHandle session, CancellationToken ct)
    {
        var collection = db.GetCollection<ProductData>("product_data");

        var indexes = new[]
        {
            new CreateIndexModel<ProductData>(
                Builders<ProductData>.IndexKeys.Ascending("_t"),
                new CreateIndexOptions { Name = "idx_type" }),

            new CreateIndexModel<ProductData>(
                Builders<ProductData>.IndexKeys.Descending(x => x.CreatedAt),
                new CreateIndexOptions { Name = "idx_created" })
        };

        await collection.Indexes.CreateManyAsync(indexes, ct);
    }

    public override Task DownAsync(IMongoDatabase db, IClientSessionHandle session, CancellationToken ct)
    {
        var collection = db.GetCollection<ProductData>("product_data");
        return collection.Indexes.DropAllAsync(ct);
    }
}
