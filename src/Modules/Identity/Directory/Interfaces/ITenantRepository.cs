namespace Identity.Directory.Interfaces;

/// <summary>
///     Repository contract for <see cref="Tenant" /> entities.
/// </summary>
public interface ITenantRepository : IRepository<Tenant>
{
    /// <summary>
    ///     Returns <c>true</c> if a tenant with the given code already exists (including soft-deleted tenants).
    /// </summary>
    Task<bool> ExistsByCodeAsync(string code, CancellationToken ct = default);
}
