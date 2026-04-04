using ErrorOr;
using Microsoft.AspNetCore.Mvc;
using ProductCatalog.Features.ProductData.CreateImageProductData;

namespace ProductCatalog.Features.ProductData;

public sealed partial class ProductDataController
{
    /// <summary>Creates a new image product-data document and returns it with a 201 Location header.</summary>
    [HttpPost("image")]
    [RequirePermission(Permission.ProductData.Create)]
    public async Task<ActionResult<ProductDataResponse>> CreateImage(
        CreateImageProductDataRequest request,
        CancellationToken ct
    )
    {
        ErrorOr<ProductDataResponse> result = await bus.InvokeAsync<ErrorOr<ProductDataResponse>>(
            new CreateImageProductDataCommand(request),
            ct
        );
        return result.ToCreatedResult(this, v => new { id = v.Id, version = this.GetApiVersion() });
    }
}
