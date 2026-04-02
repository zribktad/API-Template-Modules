using APITemplate.Application.Common.Options;
using APITemplate.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace APITemplate.Infrastructure.Persistence;

/// <summary>
/// Seeds the bootstrap tenant on application startup, creating it if absent or restoring it
/// if it was previously soft-deleted or deactivated.
/// </summary>
public sealed class AuthBootstrapSeeder
{
    private static readonly Guid DefaultTenantId = Guid.Parse(
        "00000000-0000-0000-0000-000000000001"
    );

    private readonly AppDbContext _dbContext;
    private readonly BootstrapTenantOptions _tenantOptions;

    public AuthBootstrapSeeder(
        AppDbContext dbContext,
        IOptions<BootstrapTenantOptions> tenantOptions
    )
    {
        _dbContext = dbContext;
        _tenantOptions = tenantOptions.Value;
    }

    /// <summary>
    /// Ensures the configured bootstrap tenant exists and is active.
    /// Creates, restores, or reactivates the tenant as needed, then persists any changes.
    /// </summary>
    public async Task SeedAsync(CancellationToken ct = default)
    {
        var tenantIdentity = GetTenantIdentity();
        var tenant = await FindTenantAsync(tenantIdentity.Code, ct);
        var hasChanges = tenant is null ? CreateTenant(tenantIdentity) : RestoreTenant(tenant);

        await SaveIfChangedAsync(hasChanges, ct);
    }

    private TenantIdentity GetTenantIdentity()
    {
        return new TenantIdentity(_tenantOptions.Code.Trim(), _tenantOptions.Name.Trim());
    }

    private Task<Tenant?> FindTenantAsync(string tenantCode, CancellationToken ct)
    {
        // Bypass SoftDelete and Tenant filters — seeder runs at startup without tenant context
        // and must find the tenant even if it was soft-deleted (to restore it).
        return _dbContext
            .Tenants.IgnoreQueryFilters(["SoftDelete", "Tenant"])
            .FirstOrDefaultAsync(t => t.Code == tenantCode, ct);
    }

    private bool CreateTenant(TenantIdentity tenantIdentity)
    {
        var tenant = new Tenant
        {
            Id = DefaultTenantId,
            TenantId = Guid.Empty,
            Code = tenantIdentity.Code,
            Name = tenantIdentity.Name,
            IsActive = true,
        };

        _dbContext.Tenants.Add(tenant);
        return true;
    }

    private static bool RestoreTenant(Tenant tenant)
    {
        var hasChanges = EnsureTenantIsActive(tenant);
        return EnsureTenantIsNotDeleted(tenant) || hasChanges;
    }

    private static bool EnsureTenantIsActive(Tenant tenant)
    {
        if (tenant.IsActive)
            return false;

        tenant.IsActive = true;
        return true;
    }

    private static bool EnsureTenantIsNotDeleted(Tenant tenant)
    {
        if (!tenant.IsDeleted)
            return false;

        tenant.IsDeleted = false;
        tenant.DeletedAtUtc = null;
        tenant.DeletedBy = null;
        return true;
    }

    private Task SaveIfChangedAsync(bool hasChanges, CancellationToken ct)
    {
        return hasChanges ? _dbContext.SaveChangesAsync(ct) : Task.CompletedTask;
    }

    private readonly record struct TenantIdentity(string Code, string Name);
}
