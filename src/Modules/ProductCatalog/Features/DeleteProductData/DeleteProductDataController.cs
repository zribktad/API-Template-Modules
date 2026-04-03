using Asp.Versioning;
using ErrorOr;
using Microsoft.AspNetCore.Mvc;
using SharedKernel.Contracts.Api;
using SharedKernel.Contracts.Security;
using Wolverine;

namespace ProductCatalog.Features.DeleteProductData;

[ApiVersion(1.0)]
[Route("api/v{version:apiVersion}/product-data")]
public sealed class DeleteProductDataController(IMessageBus bus) : ApiControllerBase
{
    /// <summary>Deletes a product data document by its identifier.</summary>
    [HttpDelete("{id:guid}")]
    [RequirePermission(Permission.ProductData.Delete)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        ErrorOr<Success> result = await bus.InvokeAsync<ErrorOr<Success>>(
            new DeleteProductDataCommand(id),
            ct
        );
        return result.ToNoContentResult(this);
    }
}
