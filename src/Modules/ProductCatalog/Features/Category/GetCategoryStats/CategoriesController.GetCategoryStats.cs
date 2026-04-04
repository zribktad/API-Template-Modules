using ErrorOr;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;
using ProductCatalog.Features.Category.GetCategoryStats;
using SharedKernel.Contracts.Api;
using SharedKernel.Contracts.Security;

namespace ProductCatalog.Features.Category;

public sealed partial class CategoriesController
{
    /// <summary>
    /// Returns aggregated statistics for a category via
    /// <c>get_product_category_stats(p_category_id)</c> (EF Core FromSql).
    /// </summary>
    [HttpGet("{id:guid}/stats")]
    [RequirePermission(Permission.Categories.Read)]
    [OutputCache(PolicyName = CacheTags.Categories)]
    public async Task<ActionResult<ProductCategoryStatsResponse>> GetStats(
        Guid id,
        CancellationToken ct
    )
    {
        ErrorOr<ProductCategoryStatsResponse> result = await bus.InvokeAsync<
            ErrorOr<ProductCategoryStatsResponse>
        >(new GetCategoryStatsQuery(id), ct);
        return result.ToActionResult(this);
    }
}
