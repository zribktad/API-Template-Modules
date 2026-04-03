using Asp.Versioning;
using ErrorOr;
using Microsoft.AspNetCore.Mvc;
using SharedKernel.Contracts.Api;
using SharedKernel.Contracts.Security;
using Wolverine;

namespace ProductCatalog.Features.UpdateCategories;

[ApiVersion(1.0)]
public sealed class UpdateCategoriesController(IMessageBus bus) : ApiControllerBase
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
