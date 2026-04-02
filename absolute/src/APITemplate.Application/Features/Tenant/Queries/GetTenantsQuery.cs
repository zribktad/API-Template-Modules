using APITemplate.Application.Features.Tenant.DTOs;
using APITemplate.Application.Features.Tenant.Specifications;
using APITemplate.Domain.Interfaces;
using ErrorOr;

namespace APITemplate.Application.Features.Tenant;

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
