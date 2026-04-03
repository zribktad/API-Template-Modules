using Asp.Versioning;
using ErrorOr;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;
using SharedKernel.Contracts.Api;
using SharedKernel.Contracts.Security;
using Wolverine;

namespace ProductCatalog.Features.GetProductData;

[ApiVersion(1.0)]
[Route("api/v{version:apiVersion}/product-data")]
public sealed class GetProductDataController(IMessageBus bus) : ApiControllerBase
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
