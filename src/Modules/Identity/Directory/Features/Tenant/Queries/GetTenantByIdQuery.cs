using ErrorOr;
using Identity.Directory.Features.Tenant.Specifications;

namespace Identity.Directory.Features.Tenant;

public sealed record GetTenantByIdQuery(Guid Id) : IHasId;

public sealed class GetTenantByIdQueryHandler
{
    public static async Task<ErrorOr<TenantResponse>> HandleAsync(
        GetTenantByIdQuery request,
        ITenantRepository repository,
        CancellationToken ct
    )
    {
        TenantResponse? result = await repository.FirstOrDefaultAsync(
            new TenantByIdSpecification(request.Id),
            ct
        );
        if (result is null)
            return DomainErrors.Tenants.NotFound(request.Id);

        return result;
    }
}
