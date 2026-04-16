using ErrorOr;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;
using Wolverine;
using Wolverine.Http;

namespace Reviews.Features;

public static class ReviewsHttpEndpoints
{
    private const string BaseRoute = "/api/v1/product-reviews";
    private const string ByIdRoute = $"{BaseRoute}/{{id:guid}}";
    private const string ByProductRoute = $"{BaseRoute}/by-product/{{productId:guid}}";

    [WolverineGet(BaseRoute)]
    [RequirePermission(Permission.ProductReviews.Read)]
    [OutputCache(PolicyName = CacheTags.Reviews)]
    public static async Task<IResult> GetAll(
        [FromQuery] ProductReviewFilter filter,
        IMessageBus bus,
        HttpContext httpContext,
        CancellationToken ct)
    {
        ErrorOr<PagedResponse<ProductReviewResponse>> result = await bus.InvokeAsync<
            ErrorOr<PagedResponse<ProductReviewResponse>>>(new GetProductReviewsQuery(filter), ct);
        return result.ToHttpResult(httpContext);
    }

    [WolverineGet(ByIdRoute)]
    [RequirePermission(Permission.ProductReviews.Read)]
    [OutputCache(PolicyName = CacheTags.Reviews)]
    public static async Task<IResult> GetById(
        Guid id, IMessageBus bus, HttpContext httpContext, CancellationToken ct)
    {
        ErrorOr<ProductReviewResponse> result = await bus.InvokeAsync<
            ErrorOr<ProductReviewResponse>>(new GetProductReviewByIdQuery(id), ct);
        return result.ToHttpResult(httpContext);
    }

    [WolverineGet(ByProductRoute)]
    [RequirePermission(Permission.ProductReviews.Read)]
    [OutputCache(PolicyName = CacheTags.Reviews)]
    public static async Task<IResult> GetByProductId(
        Guid productId, IMessageBus bus, HttpContext httpContext, CancellationToken ct)
    {
        ErrorOr<IReadOnlyList<ProductReviewResponse>> result = await bus.InvokeAsync<
            ErrorOr<IReadOnlyList<ProductReviewResponse>>>(new GetProductReviewsByProductIdQuery(productId), ct);
        return result.ToHttpResult(httpContext);
    }

    [WolverinePost(BaseRoute)]
    [RequirePermission(Permission.ProductReviews.Create)]
    public static async Task<IResult> Create(
        CreateProductReviewRequest request,
        IMessageBus bus,
        HttpContext httpContext,
        CancellationToken ct)
    {
        ErrorOr<ProductReviewResponse> result = await bus.InvokeAsync<
            ErrorOr<ProductReviewResponse>>(new CreateProductReviewCommand(request), ct);
        return result.IsError
            ? result.ToHttpResult(httpContext)
            : result.ToHttpCreatedResult(httpContext, $"{BaseRoute}/{result.Value.Id}");
    }

    [WolverineDelete(ByIdRoute)]
    [RequirePermission(Permission.ProductReviews.Delete)]
    public static async Task<IResult> Delete(
        Guid id, IMessageBus bus, HttpContext httpContext, CancellationToken ct)
    {
        ErrorOr<Success> result = await bus.InvokeAsync<ErrorOr<Success>>(
            new DeleteProductReviewCommand(id), ct);
        return result.ToHttpNoContentResult(httpContext);
    }
}
