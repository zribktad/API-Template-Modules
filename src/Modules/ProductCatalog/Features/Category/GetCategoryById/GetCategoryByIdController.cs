using Asp.Versioning;
using ErrorOr;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;
using SharedKernel.Contracts.Api;
using SharedKernel.Contracts.Security;
using Wolverine;

namespace ProductCatalog.Features.Category.GetCategoryById;

[ApiVersion(1.0)]
public sealed class GetCategoryByIdController(IMessageBus bus) : ApiControllerBase
{
    /// <summary>Returns a single category by its identifier, or 404 if not found.</summary>
    [HttpGet("{id:guid}")]
    [RequirePermission(Permission.Categories.Read)]
    [OutputCache(PolicyName = CacheTags.Categories)]
    public async Task<ActionResult<CategoryResponse>> GetById(Guid id, CancellationToken ct)
    {
        ErrorOr<CategoryResponse> result = await bus.InvokeAsync<ErrorOr<CategoryResponse>>(
            new GetCategoryByIdQuery(id),
            ct
        );
        return result.ToActionResult(this);
    }
}
