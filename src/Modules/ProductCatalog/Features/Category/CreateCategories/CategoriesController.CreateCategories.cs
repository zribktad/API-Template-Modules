using ErrorOr;
using Microsoft.AspNetCore.Mvc;
using ProductCatalog.Features.Category.CreateCategories;

namespace ProductCatalog.Features.Category;

public sealed partial class CategoriesController
{
    /// <summary>Creates multiple categories in a single batch operation.</summary>
    [HttpPost]
    [RequirePermission(Permission.Categories.Create)]
    public async Task<ActionResult<BatchResponse>> Create(
        CreateCategoriesRequest request,
        CancellationToken ct
    )
    {
        ErrorOr<BatchResponse> result = await bus.InvokeAsync<ErrorOr<BatchResponse>>(
            new CreateCategoriesCommand(request),
            ct
        );
        return result.ToBatchResult(this);
    }
}
