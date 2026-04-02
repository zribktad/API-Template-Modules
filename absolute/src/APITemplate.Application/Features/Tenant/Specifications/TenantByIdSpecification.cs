using APITemplate.Application.Features.Tenant.DTOs;
using APITemplate.Application.Features.Tenant.Mappings;
using Ardalis.Specification;
using TenantEntity = APITemplate.Domain.Entities.Tenant;

namespace APITemplate.Application.Features.Tenant.Specifications;

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
