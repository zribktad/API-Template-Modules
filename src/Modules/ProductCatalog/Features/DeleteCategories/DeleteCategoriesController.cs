using Asp.Versioning;
using ErrorOr;
using Microsoft.AspNetCore.Mvc;
using SharedKernel.Contracts.Api;
using SharedKernel.Contracts.Security;
using Wolverine;

namespace ProductCatalog.Features.DeleteCategories;

[ApiVersion(1.0)]
public sealed class DeleteCategoriesController(IMessageBus bus) : ApiControllerBase
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
