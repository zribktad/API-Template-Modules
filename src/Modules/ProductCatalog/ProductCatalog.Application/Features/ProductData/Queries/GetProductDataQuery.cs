using ProductCatalog.Application.Features.ProductData.Mappings;
using ProductCatalog.Domain.Interfaces;
using ErrorOr;

namespace ProductCatalog.Application.Features.ProductData;

public sealed record GetProductDataQuery(string? Type);

public sealed class GetProductDataQueryHandler
{
    public static async Task<ErrorOr<List<ProductDataResponse>>> HandleAsync(
        GetProductDataQuery request,
        IProductDataRepository repository,
        CancellationToken ct
    )
    {
        var items = await repository.GetAllAsync(request.Type, ct);
        return items.Select(item => item.ToResponse()).ToList();
    }
}


