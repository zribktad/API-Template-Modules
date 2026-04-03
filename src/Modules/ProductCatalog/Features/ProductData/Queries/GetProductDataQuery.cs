using ErrorOr;
using ProductCatalog.Features.ProductData.Mappings;
using ProductCatalog.Interfaces;

namespace ProductCatalog.Features.ProductData;

public sealed record GetProductDataQuery(string? Type);

public sealed class GetProductDataQueryHandler
{
    public static async Task<ErrorOr<List<ProductDataResponse>>> HandleAsync(
        GetProductDataQuery request,
        IProductDataRepository repository,
        CancellationToken ct
    )
    {
        List<Entities.ProductData.ProductData> items = await repository.GetAllAsync(
            request.Type,
            ct
        );
        return items.Select(item => item.ToResponse()).ToList();
    }
}
