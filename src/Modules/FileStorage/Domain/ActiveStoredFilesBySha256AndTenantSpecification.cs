using Ardalis.Specification;

namespace FileStorage.Domain;

/// <summary>
///     Counts non-soft-deleted <see cref="StoredFile" /> rows referencing the given blob hash within a tenant.
///     Used by the delete flow to decide whether the blob is orphaned.
/// </summary>
public sealed class ActiveStoredFilesBySha256AndTenantSpecification : Specification<StoredFile>
{
    public ActiveStoredFilesBySha256AndTenantSpecification(Guid tenantId, string sha256)
    {
        Query
            .IgnoreQueryFilters()
            .Where(f => f.TenantId == tenantId && f.Sha256 == sha256 && !f.IsDeleted);
    }
}
