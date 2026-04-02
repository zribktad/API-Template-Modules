using APITemplate.Domain.Entities;
using APITemplate.Domain.Interfaces;
using APITemplate.Infrastructure.Persistence;
using Ardalis.Specification;
using Microsoft.EntityFrameworkCore;

namespace APITemplate.Infrastructure.Repositories;

/// <summary>
/// EF Core repository for <see cref="Tenant"/> that bypasses the tenant global query filter
/// so tenants can be looked up by ID or code without an active tenant context.
/// </summary>
public sealed class TenantRepository : RepositoryBase<Tenant>, ITenantRepository
{
    public TenantRepository(AppDbContext dbContext)
        : base(dbContext) { }

    private IQueryable<Tenant> UnfilteredTenants => AppDb.Tenants.IgnoreQueryFilters(["Tenant"]);

    /// <summary>Applies the specification to the tenant-filter-bypassed queryable so specifications work across all tenants.</summary>
    protected override IQueryable<Tenant> ApplySpecification(
        ISpecification<Tenant> specification,
        bool evaluateCriteriaOnly = false
    )
    {
        return SpecificationEvaluator.GetQuery(
            UnfilteredTenants,
            specification,
            evaluateCriteriaOnly
        );
    }

    protected override IQueryable<TResult> ApplySpecification<TResult>(
        ISpecification<Tenant, TResult> specification
    )
    {
        return SpecificationEvaluator.GetQuery(UnfilteredTenants, specification);
    }

    public override async Task<Tenant?> GetByIdAsync<TId>(
        TId id,
        CancellationToken cancellationToken = default
    )
        where TId : default
    {
        if (id is not Guid guid)
            throw new ArgumentException(
                $"Expected Guid but received {typeof(TId).Name}.",
                nameof(id)
            );

        return await UnfilteredTenants.FirstOrDefaultAsync(t => t.Id == guid, cancellationToken);
    }

    /// <summary>
    /// Checks whether a tenant with the given code exists, bypassing both tenant and soft-delete
    /// filters to prevent reuse of codes from deleted tenants.
    /// </summary>
    public Task<bool> CodeExistsAsync(string code, CancellationToken ct = default)
    {
        return AppDb
            .Tenants.IgnoreQueryFilters(["Tenant", "SoftDelete"])
            .AnyAsync(t => t.Code == code, ct);
    }
}
