using ErrorOr;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;
using ProductCatalog.Features.ProductData.GetProductData;

namespace ProductCatalog.Features.ProductData;

public sealed partial class ProductDataController
{
    /// <summary>Returns all product data documents, optionally filtered by type.</summary>
    [HttpGet]
    [RequirePermission(Permission.ProductData.Read)]
    [OutputCache(PolicyName = CacheTags.ProductData)]
    public async Task<ActionResult<List<ProductDataResponse>>> GetAll(
        [FromQuery] string? type,
        CancellationToken ct
    )
    {
        ErrorOr<List<ProductDataResponse>> result = await bus.InvokeAsync<
            ErrorOr<List<ProductDataResponse>>
        >(new GetProductDataQuery(type), ct);
        return result.ToActionResult(this);
    }
}
