using Asp.Versioning;
using ErrorOr;
using Microsoft.AspNetCore.Mvc;
using SharedKernel.Contracts.Api;
using SharedKernel.Contracts.Security;
using Wolverine;

namespace ProductCatalog.Features.Product.DeleteProducts;

[ApiVersion(1.0)]
public sealed class DeleteProductsController(IMessageBus bus) : ApiControllerBase
{
    /// <summary>Soft-deletes multiple products in a single batch operation.</summary>
    [HttpDelete]
    [RequirePermission(Permission.Products.Delete)]
    public async Task<ActionResult<BatchResponse>> Delete(
        BatchDeleteRequest request,
        CancellationToken ct
    )
    {
        ErrorOr<BatchResponse> result = await bus.InvokeAsync<ErrorOr<BatchResponse>>(
            new DeleteProductsCommand(request),
            ct
        );
        return result.ToBatchResult(this);
    }
}
