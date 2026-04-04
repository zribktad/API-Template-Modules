using ErrorOr;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;
using ProductCatalog.Features.Product.GetProducts;

namespace ProductCatalog.Features.Product;

public sealed partial class ProductsController
{
    /// <summary>Returns a filtered, paginated product list including search facets.</summary>
    [HttpGet]
    [RequirePermission(Permission.Products.Read)]
    [OutputCache(PolicyName = CacheTags.Products)]
    public async Task<ActionResult<ProductsResponse>> GetAll(
        [FromQuery] ProductFilter filter,
        CancellationToken ct
    )
    {
        ErrorOr<ProductsResponse> result = await bus.InvokeAsync<ErrorOr<ProductsResponse>>(
            new GetProductsQuery(filter),
            ct
        );
        return result.ToActionResult(this);
    }
}
