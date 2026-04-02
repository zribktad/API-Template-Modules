using APITemplate.Application.Common.Errors;
using APITemplate.Application.Features.Tenant.DTOs;
using APITemplate.Application.Features.Tenant.Specifications;
using APITemplate.Domain.Entities.Contracts;
using APITemplate.Domain.Interfaces;
using ErrorOr;

namespace APITemplate.Application.Features.Tenant;

public sealed record GetTenantByIdQuery(Guid Id) : IHasId;

public sealed class GetTenantByIdQueryHandler
{
    public static async Task<ErrorOr<TenantResponse>> HandleAsync(
        GetTenantByIdQuery request,
        ITenantRepository repository,
        CancellationToken ct
    )
    {
        var result = await repository.FirstOrDefaultAsync(
            new TenantByIdSpecification(request.Id),
            ct
        );
        if (result is null)
            return DomainErrors.Tenants.NotFound(request.Id);

        return result;
    }
}
