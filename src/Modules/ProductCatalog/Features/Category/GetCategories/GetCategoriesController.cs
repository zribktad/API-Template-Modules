using Asp.Versioning;
using ErrorOr;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;
using SharedKernel.Contracts.Api;
using SharedKernel.Contracts.Security;
using Wolverine;

namespace ProductCatalog.Features.Category.GetCategories;

[ApiVersion(1.0)]
public sealed class GetCategoriesController(IMessageBus bus) : ApiControllerBase
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
