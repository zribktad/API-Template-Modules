using ErrorOr;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;
using ProductCatalog.Features.ProductData.GetProductDataById;
using SharedKernel.Contracts.Api;
using SharedKernel.Contracts.Security;

namespace ProductCatalog.Features.ProductData;

public sealed partial class ProductDataController
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
