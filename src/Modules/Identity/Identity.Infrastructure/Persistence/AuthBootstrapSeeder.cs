using Identity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SharedKernel.Application.Options;

namespace Identity.Infrastructure.Persistence;

/// <summary>
/// Seeds the bootstrap tenant on application startup, creating it if absent or restoring it
/// if it was previously soft-deleted or deactivated.
/// </summary>
public sealed class AuthBootstrapSeeder
{
    private static readonly Guid DefaultTenantId = Guid.Parse("00000000-0000-0000-0000-000000000001");

    private readonly IdentityDbContext _dbContext;
    private readonly BootstrapTenantOptions _tenantOptions;

    public AuthBootstrapSeeder(IdentityDbContext dbContext, IOptions<BootstrapTenantOptions> tenantOptions)
    {
        _dbContext = dbContext;
        _tenantOptions = tenantOptions.Value;
    }

    public async Task SeedAsync(CancellationToken ct = default)
    {
        TenantIdentity tenantIdentity = GetTenantIdentity();
        Tenant? tenant = await FindTenantAsync(tenantIdentity.Code, ct);
        bool hasChanges = tenant is null ? CreateTenant(tenantIdentity) : RestoreTenant(tenant);
        await SaveIfChangedAsync(hasChanges, ct);
    }

    private TenantIdentity GetTenantIdentity() =>
        new(_tenantOptions.Code.Trim(), _tenantOptions.Name.Trim());

    private Task<Tenant?> FindTenantAsync(string tenantCode, CancellationToken ct) =>
        _dbContext
            .Tenants.IgnoreQueryFilters(["SoftDelete", "Tenant"])
            .FirstOrDefaultAsync(t => t.Code == tenantCode, ct);

    private bool CreateTenant(TenantIdentity tenantIdentity)
    {
        Tenant tenant = new()
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
        bool hasChanges = EnsureTenantIsActive(tenant);
        return EnsureTenantIsNotDeleted(tenant) || hasChanges;
    }

    private static bool EnsureTenantIsActive(Tenant tenant)
    {
        if (tenant.IsActive) return false;
        tenant.IsActive = true;
        return true;
    }

    private static bool EnsureTenantIsNotDeleted(Tenant tenant)
    {
        if (!tenant.IsDeleted) return false;
        tenant.IsDeleted = false;
        tenant.DeletedAtUtc = null;
        tenant.DeletedBy = null;
        return true;
    }

    private Task SaveIfChangedAsync(bool hasChanges, CancellationToken ct) =>
        hasChanges ? _dbContext.SaveChangesAsync(ct) : Task.CompletedTask;

    private readonly record struct TenantIdentity(string Code, string Name);
}
