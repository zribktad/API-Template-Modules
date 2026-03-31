using Asp.Versioning;
using ErrorOr;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;
using Reviews.Api.Authorization;
using Reviews.Api.ErrorOrMapping;
using Wolverine;

namespace Reviews.Api.Controllers.V1;

[ApiVersion(1.0)]
public sealed class ProductReviewsController(IMessageBus bus) : ApiControllerBase
{
    [HttpGet]
    [RequirePermission(Permission.ProductReviews.Read)]
    [OutputCache(PolicyName = CacheTags.Reviews)]
    public async Task<ActionResult<PagedResponse<ProductReviewResponse>>> GetAll(
        [FromQuery] ProductReviewFilter filter,
        CancellationToken ct
    )
    {
        ErrorOr<PagedResponse<ProductReviewResponse>> result = await bus.InvokeAsync<ErrorOr<PagedResponse<ProductReviewResponse>>>(
            new GetProductReviewsQuery(filter),
            ct
        );
        return result.ToActionResult(this);
    }

    [HttpGet("{id:guid}")]
    [RequirePermission(Permission.ProductReviews.Read)]
    [OutputCache(PolicyName = CacheTags.Reviews)]
    public async Task<ActionResult<ProductReviewResponse>> GetById(Guid id, CancellationToken ct)
    {
        ErrorOr<ProductReviewResponse> result = await bus.InvokeAsync<ErrorOr<ProductReviewResponse>>(
            new GetProductReviewByIdQuery(id),
            ct
        );
        return result.ToActionResult(this);
    }

    [HttpGet("by-product/{productId:guid}")]
    [RequirePermission(Permission.ProductReviews.Read)]
    [OutputCache(PolicyName = CacheTags.Reviews)]
    public async Task<ActionResult<IReadOnlyList<ProductReviewResponse>>> GetByProductId(
        Guid productId,
        CancellationToken ct
    )
    {
        ErrorOr<IReadOnlyList<ProductReviewResponse>> result = await bus.InvokeAsync<ErrorOr<IReadOnlyList<ProductReviewResponse>>>(
            new GetProductReviewsByProductIdQuery(productId),
            ct
        );
        return result.ToActionResult(this);
    }

    [HttpPost]
    [RequirePermission(Permission.ProductReviews.Create)]
    public async Task<ActionResult<ProductReviewResponse>> Create(
        CreateProductReviewRequest request,
        CancellationToken ct
    )
    {
        ErrorOr<ProductReviewResponse> result = await bus.InvokeAsync<ErrorOr<ProductReviewResponse>>(
            new CreateProductReviewCommand(request),
            ct
        );
        return result.ToCreatedResult(this, v => new { id = v.Id, version = this.GetApiVersion() });
    }

    [HttpDelete("{id:guid}")]
    [RequirePermission(Permission.ProductReviews.Delete)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        ErrorOr<Success> result = await bus.InvokeAsync<ErrorOr<Success>>(
            new DeleteProductReviewCommand(id),
            ct
        );
        return result.ToNoContentResult(this);
    }
}
