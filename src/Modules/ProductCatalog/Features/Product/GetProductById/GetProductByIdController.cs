using Asp.Versioning;
using ErrorOr;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;
using SharedKernel.Contracts.Api;
using SharedKernel.Contracts.Security;
using Wolverine;

namespace ProductCatalog.Features.Product.GetProductById;

[ApiVersion(1.0)]
public sealed class GetProductByIdController(IMessageBus bus) : ApiControllerBase
{
    /// <summary>Returns a single product by its identifier, or 404 if not found.</summary>
    [HttpGet("{id:guid}")]
    [RequirePermission(Permission.Products.Read)]
    [OutputCache(PolicyName = CacheTags.Products)]
    public async Task<ActionResult<ProductResponse>> GetById(Guid id, CancellationToken ct)
    {
        ErrorOr<ProductResponse> result = await bus.InvokeAsync<ErrorOr<ProductResponse>>(
            new GetProductByIdQuery(id),
            ct
        );
        return result.ToActionResult(this);
    }
}
