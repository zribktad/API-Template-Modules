using ErrorOr;
using Microsoft.AspNetCore.Mvc;
using ProductCatalog.Features.Product.DeleteProducts;

namespace ProductCatalog.Features.Product;

public sealed partial class ProductsController
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
