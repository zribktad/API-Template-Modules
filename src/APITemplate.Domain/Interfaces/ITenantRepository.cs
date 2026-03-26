using APITemplate.Domain.Entities;

namespace APITemplate.Domain.Interfaces;

/// <summary>
/// Repository contract for <see cref="Tenant"/> entities with tenant-specific lookup operations.
/// </summary>
public interface ITenantRepository : IRepository<Tenant>
{
    /// <summary>
    /// Returns <c>true</c> if a tenant with the given code already exists, enabling uniqueness validation before creation.
    /// </summary>
    Task<bool> CodeExistsAsync(string code, CancellationToken ct = default);
}
