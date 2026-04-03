using ErrorOr;
using ProductCatalog.Features.ProductData.Mappings;
using ProductCatalog.Interfaces;
using SharedKernel.Application.Context;

namespace ProductCatalog.Features.ProductData;

public sealed record GetProductDataByIdQuery(Guid Id) : IHasId;

public sealed class GetProductDataByIdQueryHandler
{
    public static async Task<ErrorOr<ProductDataResponse>> HandleAsync(
        GetProductDataByIdQuery request,
        IProductDataRepository repository,
        ITenantProvider tenantProvider,
        CancellationToken ct
    )
    {
        Guid tenantId = tenantProvider.TenantId;
        Domain.Entities.ProductData.ProductData? data = await repository.GetByIdAsync(
            request.Id,
            ct
        );

        if (data is null || data.TenantId != tenantId)
            return DomainErrors.ProductData.NotFound(request.Id);

        return data.ToResponse();
    }
}

