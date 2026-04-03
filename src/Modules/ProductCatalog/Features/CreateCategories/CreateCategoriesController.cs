using Asp.Versioning;
using ErrorOr;
using Microsoft.AspNetCore.Mvc;
using SharedKernel.Contracts.Api;
using SharedKernel.Contracts.Security;
using Wolverine;

namespace ProductCatalog.Features.CreateCategories;

[ApiVersion(1.0)]
public sealed class CreateCategoriesController(IMessageBus bus) : ApiControllerBase
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
