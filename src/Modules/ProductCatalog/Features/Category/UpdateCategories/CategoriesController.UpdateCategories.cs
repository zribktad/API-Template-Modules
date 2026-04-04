using ErrorOr;
using Microsoft.AspNetCore.Mvc;
using ProductCatalog.Features.Category.UpdateCategories;

namespace ProductCatalog.Features.Category;

public sealed partial class CategoriesController
{
    /// <summary>Updates multiple categories in a single batch operation.</summary>
    [HttpPut]
    [RequirePermission(Permission.Categories.Update)]
    public async Task<ActionResult<BatchResponse>> Update(
        UpdateCategoriesRequest request,
        CancellationToken ct
    )
    {
        ErrorOr<BatchResponse> result = await bus.InvokeAsync<ErrorOr<BatchResponse>>(
            new UpdateCategoriesCommand(request),
            ct
        );
        return result.ToBatchResult(this);
    }
}
