using APITemplate.Domain.Entities;

namespace APITemplate.Domain.Interfaces;

/// <summary>
/// Repository contract for <see cref="ProductData.ProductData"/> documents stored in MongoDB.
/// Provides CRUD and soft-delete operations scoped to the current tenant.
/// </summary>
public interface IProductDataRepository
{
    /// <summary>Returns the product-data document with the given ID, or <c>null</c> if not found or soft-deleted.</summary>
    Task<ProductData?> GetByIdAsync(Guid id, CancellationToken ct = default);

    /// <summary>Returns all non-deleted product-data documents whose IDs are in the provided collection.</summary>
    Task<List<ProductData>> GetByIdsAsync(IEnumerable<Guid> ids, CancellationToken ct = default);

    /// <summary>
    /// Returns all non-deleted product-data documents, optionally filtered by discriminator <paramref name="type"/> (e.g. "image" or "video").
    /// </summary>
    Task<List<ProductData>> GetAllAsync(string? type = null, CancellationToken ct = default);

    /// <summary>Inserts a new product-data document and returns the persisted instance.</summary>
    Task<ProductData> CreateAsync(ProductData productData, CancellationToken ct = default);

    /// <summary>Soft-deletes the product-data document with the given ID, recording the actor and timestamp.</summary>
    Task SoftDeleteAsync(
        Guid id,
        Guid actorId,
        DateTime deletedAtUtc,
        CancellationToken ct = default
    );

    /// <summary>
    /// Soft-deletes all product-data documents belonging to the specified tenant and returns the count of affected documents.
    /// </summary>
    Task<long> SoftDeleteByTenantAsync(
        Guid tenantId,
        Guid actorId,
        DateTime deletedAtUtc,
        CancellationToken ct = default
    );
}
