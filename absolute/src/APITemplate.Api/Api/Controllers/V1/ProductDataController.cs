using APITemplate.Api.Authorization;
using APITemplate.Api.Controllers;
using APITemplate.Api.ErrorOrMapping;
using APITemplate.Application.Common.Events;
using APITemplate.Application.Common.Security;
using Asp.Versioning;
using ErrorOr;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;
using Wolverine;

namespace APITemplate.Api.Controllers.V1;

[ApiVersion(1.0)]
[Route("api/v{version:apiVersion}/product-data")]
/// <summary>
/// Presentation-layer controller that manages product supplementary data (images and videos)
/// stored in MongoDB, with output-cache integration for read endpoints.
/// </summary>
public sealed class ProductDataController(IMessageBus bus) : ApiControllerBase
{
    /// <summary>Returns all product data documents, optionally filtered by type.</summary>
    [HttpGet]
    [RequirePermission(Permission.ProductData.Read)]
    [OutputCache(PolicyName = CacheTags.ProductData)]
    public async Task<ActionResult<List<ProductDataResponse>>> GetAll(
        [FromQuery] string? type,
        CancellationToken ct
    )
    {
        var result = await bus.InvokeAsync<ErrorOr<List<ProductDataResponse>>>(
            new GetProductDataQuery(type),
            ct
        );
        return result.ToActionResult(this);
    }

    /// <summary>Returns a single product data document by its identifier, or 404 if not found.</summary>
    [HttpGet("{id:guid}")]
    [RequirePermission(Permission.ProductData.Read)]
    [OutputCache(PolicyName = CacheTags.ProductData)]
    public async Task<ActionResult<ProductDataResponse>> GetById(Guid id, CancellationToken ct)
    {
        var result = await bus.InvokeAsync<ErrorOr<ProductDataResponse>>(
            new GetProductDataByIdQuery(id),
            ct
        );
        return result.ToActionResult(this);
    }

    /// <summary>Creates a new image product-data document and returns it with a 201 Location header.</summary>
    [HttpPost("image")]
    [RequirePermission(Permission.ProductData.Create)]
    public async Task<ActionResult<ProductDataResponse>> CreateImage(
        CreateImageProductDataRequest request,
        CancellationToken ct
    )
    {
        var result = await bus.InvokeAsync<ErrorOr<ProductDataResponse>>(
            new CreateImageProductDataCommand(request),
            ct
        );
        return result.ToCreatedResult(this, v => new { id = v.Id, version = this.GetApiVersion() });
    }

    /// <summary>Creates a new video product-data document and returns it with a 201 Location header.</summary>
    [HttpPost("video")]
    [RequirePermission(Permission.ProductData.Create)]
    public async Task<ActionResult<ProductDataResponse>> CreateVideo(
        CreateVideoProductDataRequest request,
        CancellationToken ct
    )
    {
        var result = await bus.InvokeAsync<ErrorOr<ProductDataResponse>>(
            new CreateVideoProductDataCommand(request),
            ct
        );
        return result.ToCreatedResult(this, v => new { id = v.Id, version = this.GetApiVersion() });
    }

    /// <summary>Deletes a product data document by its identifier.</summary>
    [HttpDelete("{id:guid}")]
    [RequirePermission(Permission.ProductData.Delete)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var result = await bus.InvokeAsync<ErrorOr<Success>>(new DeleteProductDataCommand(id), ct);
        return result.ToNoContentResult(this);
    }
}
