using Asp.Versioning;
using ErrorOr;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;
using SharedKernel.Contracts.Api;
using SharedKernel.Contracts.Security;
using Wolverine;

namespace ProductCatalog.Features.GetProductDataById;

[ApiVersion(1.0)]
[Route("api/v{version:apiVersion}/product-data")]
public sealed class GetProductDataByIdController(IMessageBus bus) : ApiControllerBase
{
    /// <summary>Returns a single product data document by its identifier, or 404 if not found.</summary>
    [HttpGet("{id:guid}")]
    [RequirePermission(Permission.ProductData.Read)]
    [OutputCache(PolicyName = CacheTags.ProductData)]
    public async Task<ActionResult<ProductDataResponse>> GetById(Guid id, CancellationToken ct)
    {
        ErrorOr<ProductDataResponse> result = await bus.InvokeAsync<ErrorOr<ProductDataResponse>>(
            new GetProductDataByIdQuery(id),
            ct
        );
        return result.ToActionResult(this);
    }
}
