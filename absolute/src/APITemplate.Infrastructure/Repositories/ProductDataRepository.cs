using APITemplate.Application.Common.Context;
using APITemplate.Domain.Entities;
using APITemplate.Domain.Interfaces;
using APITemplate.Infrastructure.Persistence;
using MongoDB.Driver;

namespace APITemplate.Infrastructure.Repositories;

/// <summary>
/// MongoDB repository for <see cref="ProductData"/> documents, applying tenant and soft-delete
/// isolation at the query level since MongoDB has no EF Core global filter equivalent.
/// </summary>
public sealed class ProductDataRepository : IProductDataRepository
{
    private readonly IMongoCollection<ProductData> _collection;
    private readonly ITenantProvider _tenantProvider;

    public ProductDataRepository(MongoDbContext context, ITenantProvider tenantProvider)
    {
        _collection = context.ProductData;
        _tenantProvider = tenantProvider;
    }

    /// <summary>Returns a single non-deleted document matching the given ID within the current tenant, or <c>null</c> if not found.</summary>
    public async Task<ProductData?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        await _collection
            .Find(x => x.Id == id && x.TenantId == _tenantProvider.TenantId && !x.IsDeleted)
            .FirstOrDefaultAsync(ct);

    /// <summary>Returns non-deleted documents for the given IDs within the current tenant; deduplicates the ID list before querying.</summary>
    public async Task<List<ProductData>> GetByIdsAsync(
        IEnumerable<Guid> ids,
        CancellationToken ct = default
    )
    {
        var idArray = ids.Distinct().ToArray();

        if (idArray.Length == 0)
            return [];

        return await _collection
            .Find(
                Builders<ProductData>.Filter.And(
                    Builders<ProductData>.Filter.In(x => x.Id, idArray),
                    Builders<ProductData>.Filter.Eq(x => x.TenantId, _tenantProvider.TenantId),
                    Builders<ProductData>.Filter.Eq(x => x.IsDeleted, false)
                )
            )
            .ToListAsync(ct);
    }

    /// <summary>Returns all non-deleted documents for the current tenant, optionally filtered by the MongoDB discriminator type.</summary>
    public async Task<List<ProductData>> GetAllAsync(
        string? type = null,
        CancellationToken ct = default
    )
    {
        var filter = type is null
            ? Builders<ProductData>.Filter.And(
                Builders<ProductData>.Filter.Eq(x => x.TenantId, _tenantProvider.TenantId),
                Builders<ProductData>.Filter.Eq(x => x.IsDeleted, false)
            )
            : Builders<ProductData>.Filter.And(
                Builders<ProductData>.Filter.Eq(x => x.TenantId, _tenantProvider.TenantId),
                Builders<ProductData>.Filter.Eq("_t", type),
                Builders<ProductData>.Filter.Eq(x => x.IsDeleted, false)
            );

        return await _collection.Find(filter).ToListAsync(ct);
    }

    /// <summary>Inserts a new document into the collection and returns the inserted document.</summary>
    public async Task<ProductData> CreateAsync(
        ProductData productData,
        CancellationToken ct = default
    )
    {
        await _collection.InsertOneAsync(productData, cancellationToken: ct);
        return productData;
    }

    /// <summary>Soft-deletes a single document by setting <c>IsDeleted</c>, <c>DeletedAtUtc</c>, and <c>DeletedBy</c>.</summary>
    public async Task SoftDeleteAsync(
        Guid id,
        Guid actorId,
        DateTime deletedAtUtc,
        CancellationToken ct = default
    )
    {
        var update = Builders<ProductData>
            .Update.Set(x => x.IsDeleted, true)
            .Set(x => x.DeletedAtUtc, deletedAtUtc)
            .Set(x => x.DeletedBy, actorId);

        await _collection.UpdateOneAsync(
            x => x.Id == id && x.TenantId == _tenantProvider.TenantId && !x.IsDeleted,
            update,
            cancellationToken: ct
        );
    }

    /// <summary>
    /// Soft-deletes all non-deleted documents belonging to the specified tenant in a single
    /// <c>UpdateMany</c> operation and returns the count of modified documents.
    /// </summary>
    public async Task<long> SoftDeleteByTenantAsync(
        Guid tenantId,
        Guid actorId,
        DateTime deletedAtUtc,
        CancellationToken ct = default
    )
    {
        var filter = Builders<ProductData>.Filter.And(
            Builders<ProductData>.Filter.Eq(x => x.TenantId, tenantId),
            Builders<ProductData>.Filter.Eq(x => x.IsDeleted, false)
        );

        var update = Builders<ProductData>
            .Update.Set(x => x.IsDeleted, true)
            .Set(x => x.DeletedAtUtc, deletedAtUtc)
            .Set(x => x.DeletedBy, actorId);

        var result = await _collection.UpdateManyAsync(filter, update, cancellationToken: ct);
        return result.ModifiedCount;
    }
}
