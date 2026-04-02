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
/// <summary>
/// Presentation-layer controller that exposes full CRUD endpoints for the product catalog,
/// with permission-based authorization and tenant-aware output caching.
/// </summary>
public sealed class ProductsController(IMessageBus bus) : ApiControllerBase
{
    /// <summary>Returns a filtered, paginated product list including search facets.</summary>
    [HttpGet]
    [RequirePermission(Permission.Products.Read)]
    [OutputCache(PolicyName = CacheTags.Products)]
    public async Task<ActionResult<ProductsResponse>> GetAll(
        [FromQuery] ProductFilter filter,
        CancellationToken ct
    )
    {
        var result = await bus.InvokeAsync<ErrorOr<ProductsResponse>>(
            new GetProductsQuery(filter),
            ct
        );
        return result.ToActionResult(this);
    }

    /// <summary>Returns a single product by its identifier, or 404 if not found.</summary>
    [HttpGet("{id:guid}")]
    [RequirePermission(Permission.Products.Read)]
    [OutputCache(PolicyName = CacheTags.Products)]
    public async Task<ActionResult<ProductResponse>> GetById(Guid id, CancellationToken ct)
    {
        var result = await bus.InvokeAsync<ErrorOr<ProductResponse>>(
            new GetProductByIdQuery(id),
            ct
        );
        return result.ToActionResult(this);
    }

    /// <summary>Creates multiple products in a single batch operation.</summary>
    [HttpPost]
    [RequirePermission(Permission.Products.Create)]
    public async Task<ActionResult<BatchResponse>> Create(
        CreateProductsRequest request,
        CancellationToken ct
    )
    {
        var result = await bus.InvokeAsync<ErrorOr<BatchResponse>>(
            new CreateProductsCommand(request),
            ct
        );
        return result.ToBatchResult(this);
    }

    /// <summary>Updates multiple products in a single batch operation.</summary>
    [HttpPut]
    [RequirePermission(Permission.Products.Update)]
    public async Task<ActionResult<BatchResponse>> Update(
        UpdateProductsRequest request,
        CancellationToken ct
    )
    {
        var result = await bus.InvokeAsync<ErrorOr<BatchResponse>>(
            new UpdateProductsCommand(request),
            ct
        );
        return result.ToBatchResult(this);
    }

    /// <summary>Soft-deletes multiple products in a single batch operation.</summary>
    [HttpDelete]
    [RequirePermission(Permission.Products.Delete)]
    public async Task<ActionResult<BatchResponse>> Delete(
        BatchDeleteRequest request,
        CancellationToken ct
    )
    {
        var result = await bus.InvokeAsync<ErrorOr<BatchResponse>>(
            new DeleteProductsCommand(request),
            ct
        );
        return result.ToBatchResult(this);
    }
}
