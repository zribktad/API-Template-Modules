using ErrorOr;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;
using ProductCatalog.Features.Category.GetCategories;

namespace ProductCatalog.Features.Category;

public sealed partial class CategoriesController
{
    /// <summary>Returns a paginated, filterable list of categories from the output cache.</summary>
    [HttpGet]
    [RequirePermission(Permission.Categories.Read)]
    [OutputCache(PolicyName = CacheTags.Categories)]
    public async Task<ActionResult<PagedResponse<CategoryResponse>>> GetAll(
        [FromQuery] CategoryFilter filter,
        CancellationToken ct
    )
    {
        ErrorOr<PagedResponse<CategoryResponse>> result = await bus.InvokeAsync<
            ErrorOr<PagedResponse<CategoryResponse>>
        >(new GetCategoriesQuery(filter), ct);
        return result.ToActionResult(this);
    }
}
