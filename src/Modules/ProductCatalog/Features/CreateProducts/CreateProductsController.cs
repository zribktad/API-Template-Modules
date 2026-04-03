using Asp.Versioning;
using ErrorOr;
using Microsoft.AspNetCore.Mvc;
using SharedKernel.Contracts.Api;
using SharedKernel.Contracts.Security;
using Wolverine;

namespace ProductCatalog.Features.CreateProducts;

[ApiVersion(1.0)]
public sealed class CreateProductsController(IMessageBus bus) : ApiControllerBase
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
