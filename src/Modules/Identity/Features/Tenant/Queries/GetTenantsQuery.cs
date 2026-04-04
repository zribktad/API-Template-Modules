using ErrorOr;
using Identity.Features.Tenant.Specifications;

namespace Identity.Features.Tenant;

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
