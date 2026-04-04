using ErrorOr;
using Microsoft.AspNetCore.Mvc;
using ProductCatalog.Features.Product.CreateProducts;
using SharedKernel.Contracts.Api;
using SharedKernel.Contracts.Security;

namespace ProductCatalog.Features.Product;

public sealed partial class ProductsController
{
    /// <summary>Creates multiple products in a single batch operation.</summary>
    [HttpPost]
    [RequirePermission(Permission.Products.Create)]
    public async Task<ActionResult<BatchResponse>> Create(
        CreateProductsRequest request,
        CancellationToken ct
    )
    {
        ErrorOr<BatchResponse> result = await bus.InvokeAsync<ErrorOr<BatchResponse>>(
            new CreateProductsCommand(request),
            ct
        );
        return result.ToBatchResult(this);
    }
}
