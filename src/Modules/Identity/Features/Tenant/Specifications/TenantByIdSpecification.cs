using Identity.Features.Tenant.DTOs;
using Identity.Features.Tenant.Mappings;
using Ardalis.Specification;
using TenantEntity = Identity.Entities.Tenant;

namespace Identity.Features.Tenant.Specifications;

/// <summary>
/// Ardalis specification that fetches a single tenant by ID and projects it to <see cref="TenantResponse"/>.
/// </summary>
public sealed class TenantByIdSpecification : Specification<TenantEntity, TenantResponse>
{
    /// <summary>
    /// Initialises the specification to match the tenant with the given <paramref name="id"/> and apply the response projection.
    /// </summary>
    public TenantByIdSpecification(Guid id)
    {
        Query.Where(tenant => tenant.Id == id).AsNoTracking().Select(TenantMappings.Projection);
    }
}

