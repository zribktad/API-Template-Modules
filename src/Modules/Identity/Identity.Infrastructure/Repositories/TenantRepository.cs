using Ardalis.Specification;
using Identity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Identity.Infrastructure.Repositories;

/// <summary>
/// EF Core repository for <see cref="Tenant"/> that bypasses the tenant global query filter
/// so tenants can be looked up by ID or code without an active tenant context.
/// </summary>
public sealed class TenantRepository : RepositoryBase<Tenant>, ITenantRepository
{
    private readonly IdentityDbContext _db;

    public TenantRepository(IdentityDbContext dbContext)
        : base(dbContext)
    {
        _db = dbContext;
    }

    private IQueryable<Tenant> UnfilteredTenants =>
        _db.Tenants.IgnoreQueryFilters([GlobalQueryFilterNames.Tenant]);

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
    {
        if (id is not Guid guid)
            throw new ArgumentException(
                $"Expected Guid but received {typeof(TId).Name}.",
                nameof(id)
            );

        return await UnfilteredTenants
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == guid, cancellationToken);
    }
}
