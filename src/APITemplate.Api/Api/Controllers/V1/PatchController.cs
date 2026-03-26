using APITemplate.Api.Authorization;
using APITemplate.Api.Controllers;
using APITemplate.Api.ErrorOrMapping;
using APITemplate.Application.Features.Examples;
using APITemplate.Application.Features.Examples.DTOs;
using Asp.Versioning;
using ErrorOr;
using Microsoft.AspNetCore.Mvc;
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
    /// <summary>
    /// Applies a JSON Patch document to the specified product by passing an apply-delegate
    /// to the application layer, which mutates the DTO before persisting.
    /// </summary>
    [HttpPatch("products/{id:guid}")]
    [RequirePermission(Permission.Examples.Update)]
    public async Task<ActionResult<ProductResponse>> PatchProduct(
        Guid id,
        [FromBody] JsonPatchDocument<PatchableProductDto> patchDocument,
        CancellationToken ct
    )
    {
        var result = await bus.InvokeAsync<ErrorOr<ProductResponse>>(
            new PatchProductCommand(id, dto => patchDocument.ApplyTo(dto)),
            ct
        );
        return result.ToActionResult(this);
    }
}
