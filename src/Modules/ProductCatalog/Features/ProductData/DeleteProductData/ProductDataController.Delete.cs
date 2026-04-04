using ErrorOr;
using Microsoft.AspNetCore.Mvc;
using ProductCatalog.Features.ProductData.DeleteProductData;

namespace ProductCatalog.Features.ProductData;

public sealed partial class ProductDataController
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
