using Asp.Versioning;
using ErrorOr;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;
using SharedKernel.Contracts.Api;
using SharedKernel.Contracts.Security;
using Wolverine;

namespace ProductCatalog.Features.GetProducts;

[ApiVersion(1.0)]
public sealed class GetProductsController(IMessageBus bus) : ApiControllerBase
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
