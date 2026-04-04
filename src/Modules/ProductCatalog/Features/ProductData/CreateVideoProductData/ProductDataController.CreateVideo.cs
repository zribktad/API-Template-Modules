using ErrorOr;
using Microsoft.AspNetCore.Mvc;
using ProductCatalog.Features.ProductData.CreateVideoProductData;
using SharedKernel.Contracts.Api;
using SharedKernel.Contracts.Security;

namespace ProductCatalog.Features.ProductData;

public sealed partial class ProductDataController
{
    /// <summary>Creates a new video product-data document and returns it with a 201 Location header.</summary>
    [HttpPost("video")]
    [RequirePermission(Permission.ProductData.Create)]
    public async Task<ActionResult<ProductDataResponse>> CreateVideo(
        CreateVideoProductDataRequest request,
        CancellationToken ct
    )
    {
        ErrorOr<ProductDataResponse> result = await bus.InvokeAsync<ErrorOr<ProductDataResponse>>(
            new CreateVideoProductDataCommand(request),
            ct
        );
        return result.ToCreatedResult(this, v => new { id = v.Id, version = this.GetApiVersion() });
    }
}
