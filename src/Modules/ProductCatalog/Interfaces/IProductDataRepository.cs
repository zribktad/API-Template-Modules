namespace ProductCatalog.Interfaces;

/// <summary>
///     Repository contract for <see cref="ProductCatalog.Entities.ProductData.ProductData" /> documents stored in MongoDB.
///     Provides CRUD and soft-delete operations scoped to the current tenant.
/// </summary>
public interface IProductDataRepository
{
    /// <summary>Returns the product-data document with the given ID, or <c>null</c> if not found or soft-deleted.</summary>
    public Task<ProductData?> GetByIdAsync(Guid id, CancellationToken ct = default);

    /// <summary>Returns all non-deleted product-data documents whose IDs are in the provided collection.</summary>
    public Task<List<ProductData>> GetByIdsAsync(
        IEnumerable<Guid> ids,
        CancellationToken ct = default
    );

    /// <summary>
    ///     Returns all non-deleted product-data documents, optionally filtered by discriminator <paramref name="type" /> (e.g.
    ///     "image" or "video").
    /// </summary>
    public Task<List<ProductData>> GetAllAsync(string? type = null, CancellationToken ct = default);

    /// <summary>Inserts a new product-data document and returns the persisted instance.</summary>
    public Task<ProductData> CreateAsync(ProductData productData, CancellationToken ct = default);

    /// <summary>Soft-deletes the product-data document with the given ID, recording the actor and timestamp.</summary>
    public Task<bool> SoftDeleteAsync(
        Guid id,
        Guid actorId,
        DateTime deletedAtUtc,
        CancellationToken ct = default
    );

    /// <summary>
    ///     Soft-deletes all product-data documents belonging to the specified tenant and returns the count of affected
    ///     documents.
    /// </summary>
    public Task<long> SoftDeleteByTenantAsync(
        Guid tenantId,
        Guid actorId,
        DateTime deletedAtUtc,
        CancellationToken ct = default
    );
}
