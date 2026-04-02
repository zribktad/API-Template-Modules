using Identity.Application.Features.Tenant.DTOs;
using Identity.Application.Features.Tenant.Specifications;
using Identity.Domain.Interfaces;
using SharedKernel.Domain.Common;
using ErrorOr;

namespace Identity.Application.Features.Tenant;

public sealed record GetTenantsQuery(TenantFilter Filter);

public sealed class GetTenantsQueryHandler
{
    public static async Task<ErrorOr<PagedResponse<TenantResponse>>> HandleAsync(
        GetTenantsQuery request,
        ITenantRepository repository,
        CancellationToken ct
    )
    {
        return await repository.GetPagedAsync(
            new TenantSpecification(request.Filter),
            request.Filter.PageNumber,
            request.Filter.PageSize,
            ct
        );
    }
}
