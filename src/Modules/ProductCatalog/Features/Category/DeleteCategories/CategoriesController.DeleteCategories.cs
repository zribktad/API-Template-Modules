using ErrorOr;
using Microsoft.AspNetCore.Mvc;
using ProductCatalog.Features.Category.DeleteCategories;
using SharedKernel.Contracts.Api;
using SharedKernel.Contracts.Security;

namespace ProductCatalog.Features.Category;

public sealed partial class CategoriesController
{
    /// <summary>Soft-deletes multiple categories in a single batch operation.</summary>
    [HttpDelete]
    [RequirePermission(Permission.Categories.Delete)]
    public async Task<ActionResult<BatchResponse>> Delete(
        BatchDeleteRequest request,
        CancellationToken ct
    )
    {
        ErrorOr<BatchResponse> result = await bus.InvokeAsync<ErrorOr<BatchResponse>>(
            new DeleteCategoriesCommand(request),
            ct
        );
        return result.ToBatchResult(this);
    }
}
