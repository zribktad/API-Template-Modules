using ErrorOr;

namespace ProductCatalog.Features.ProductData.GetProductData;

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
