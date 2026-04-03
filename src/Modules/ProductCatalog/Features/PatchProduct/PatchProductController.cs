using Asp.Versioning;
using ErrorOr;
using Microsoft.AspNetCore.Mvc;
using SharedKernel.Contracts.Api;
using SharedKernel.Contracts.Security;
using SystemTextJsonPatch;
using Wolverine;

namespace ProductCatalog.Features.PatchProduct;

[ApiVersion(1.0)]
public sealed class PatchProductController(IMessageBus bus) : ApiControllerBase
{
    [HttpPatch("products/{id:guid}")]
    [RequirePermission(Permission.Examples.Update)]
    public async Task<ActionResult<ProductResponse>> PatchProduct(
        Guid id,
        [FromBody] JsonPatchDocument<PatchableProductDto> patchDocument,
        CancellationToken ct
    )
    {
        ErrorOr<ProductResponse> result = await bus.InvokeAsync<ErrorOr<ProductResponse>>(
            new PatchProductCommand(id, patchDocument),
            ct
        );
        return result.ToActionResult(this);
    }
}
