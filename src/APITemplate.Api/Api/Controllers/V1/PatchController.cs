using APITemplate.Api.Features.Examples.Commands;
using APITemplate.Api.Features.Examples.DTOs;
using Asp.Versioning;
using Contracts.Api;
using Contracts.Security;
using ErrorOr;
using Microsoft.AspNetCore.Mvc;
using ProductCatalog.Application.Features.Product.DTOs;
using SystemTextJsonPatch;
using Wolverine;

namespace APITemplate.Api.Controllers.V1;

[ApiVersion(1.0)]
/// <summary>
/// Presentation-layer controller that demonstrates JSON Patch (RFC 6902) support
/// for partial product updates using <c>SystemTextJsonPatch</c>.
/// </summary>
public sealed class PatchController(IMessageBus bus) : ApiControllerBase
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
            new PatchProductCommand(id, dto => patchDocument.ApplyTo(dto)),
            ct
        );
        return result.ToActionResult(this);
    }
}
