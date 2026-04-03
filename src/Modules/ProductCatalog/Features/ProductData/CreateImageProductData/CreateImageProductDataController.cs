using Asp.Versioning;
using ErrorOr;
using Microsoft.AspNetCore.Mvc;
using SharedKernel.Contracts.Api;
using SharedKernel.Contracts.Security;
using Wolverine;

namespace ProductCatalog.Features.ProductData.CreateImageProductData;

[ApiVersion(1.0)]
[Route("api/v{version:apiVersion}/product-data")]
public sealed class CreateImageProductDataController(IMessageBus bus) : ApiControllerBase
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
