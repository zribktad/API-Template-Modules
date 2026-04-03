using Asp.Versioning;
using ErrorOr;
using Microsoft.AspNetCore.Mvc;
using SharedKernel.Contracts.Api;
using SharedKernel.Contracts.Security;
using Wolverine;

namespace ProductCatalog.Features.UpdateProducts;

[ApiVersion(1.0)]
public sealed class UpdateProductsController(IMessageBus bus) : ApiControllerBase
{
    /// <summary>Updates multiple products in a single batch operation.</summary>
    [HttpPut]
    [RequirePermission(Permission.Products.Update)]
    public async Task<ActionResult<BatchResponse>> Update(
        UpdateProductsRequest request,
        CancellationToken ct
    )
    {
        ErrorOr<BatchResponse> result = await bus.InvokeAsync<ErrorOr<BatchResponse>>(
            new UpdateProductsCommand(request),
            ct
        );
        return result.ToBatchResult(this);
    }
}
