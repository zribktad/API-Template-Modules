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
/// Presentation-layer controller that exposes CRUD endpoints for product categories,
/// including a stored-procedure-backed statistics query.
/// </summary>
public sealed class CategoriesController(IMessageBus bus) : ApiControllerBase
{
    /// <summary>
    /// Returns a paginated, filterable list of categories from the output cache.
    /// </summary>
    [HttpGet]
    [RequirePermission(Permission.Categories.Read)]
    [OutputCache(PolicyName = CacheTags.Categories)]
    public async Task<ActionResult<PagedResponse<CategoryResponse>>> GetAll(
        [FromQuery] CategoryFilter filter,
        CancellationToken ct
    )
    {
        var result = await bus.InvokeAsync<ErrorOr<PagedResponse<CategoryResponse>>>(
            new GetCategoriesQuery(filter),
            ct
        );
        return result.ToActionResult(this);
    }

    /// <summary>Returns a single category by its identifier, or 404 if not found.</summary>
    [HttpGet("{id:guid}")]
    [RequirePermission(Permission.Categories.Read)]
    [OutputCache(PolicyName = CacheTags.Categories)]
    public async Task<ActionResult<CategoryResponse>> GetById(Guid id, CancellationToken ct)
    {
        var result = await bus.InvokeAsync<ErrorOr<CategoryResponse>>(
            new GetCategoryByIdQuery(id),
            ct
        );
        return result.ToActionResult(this);
    }

    /// <summary>Creates multiple categories in a single batch operation.</summary>
    [HttpPost]
    [RequirePermission(Permission.Categories.Create)]
    public async Task<ActionResult<BatchResponse>> Create(
        CreateCategoriesRequest request,
        CancellationToken ct
    )
    {
        var result = await bus.InvokeAsync<ErrorOr<BatchResponse>>(
            new CreateCategoriesCommand(request),
            ct
        );
        return result.ToBatchResult(this);
    }

    /// <summary>Updates multiple categories in a single batch operation.</summary>
    [HttpPut]
    [RequirePermission(Permission.Categories.Update)]
    public async Task<ActionResult<BatchResponse>> Update(
        UpdateCategoriesRequest request,
        CancellationToken ct
    )
    {
        var result = await bus.InvokeAsync<ErrorOr<BatchResponse>>(
            new UpdateCategoriesCommand(request),
            ct
        );
        return result.ToBatchResult(this);
    }

    /// <summary>Soft-deletes multiple categories in a single batch operation.</summary>
    [HttpDelete]
    [RequirePermission(Permission.Categories.Delete)]
    public async Task<ActionResult<BatchResponse>> Delete(
        BatchDeleteRequest request,
        CancellationToken ct
    )
    {
        var result = await bus.InvokeAsync<ErrorOr<BatchResponse>>(
            new DeleteCategoriesCommand(request),
            ct
        );
        return result.ToBatchResult(this);
    }

    /// <summary>
    /// Returns aggregated statistics for a category by calling the
    /// <c>get_product_category_stats(p_category_id)</c> stored procedure via EF Core FromSql.
    /// </summary>
    [HttpGet("{id:guid}/stats")]
    [RequirePermission(Permission.Categories.Read)]
    [OutputCache(PolicyName = CacheTags.Categories)]
    public async Task<ActionResult<ProductCategoryStatsResponse>> GetStats(
        Guid id,
        CancellationToken ct
    )
    {
        var result = await bus.InvokeAsync<ErrorOr<ProductCategoryStatsResponse>>(
            new GetCategoryStatsQuery(id),
            ct
        );
        return result.ToActionResult(this);
    }
}
