using Asp.Versioning;
using ErrorOr;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;
using SharedKernel.Contracts.Api;
using SharedKernel.Contracts.Security;
using Wolverine;

namespace ProductCatalog.Features.Category.GetCategoryStats;

[ApiVersion(1.0)]
public sealed class GetCategoryStatsController(IMessageBus bus) : ApiControllerBase
{
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
        ErrorOr<ProductCategoryStatsResponse> result = await bus.InvokeAsync<
            ErrorOr<ProductCategoryStatsResponse>
        >(new GetCategoryStatsQuery(id), ct);
        return result.ToActionResult(this);
    }
}
