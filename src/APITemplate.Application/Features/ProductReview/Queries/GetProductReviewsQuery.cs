using APITemplate.Application.Features.ProductReview.Specifications;
using APITemplate.Domain.Interfaces;
using ErrorOr;

namespace APITemplate.Application.Features.ProductReview;

/// <summary>Returns a paginated, filtered, and sorted list of product reviews.</summary>
public sealed record GetProductReviewsQuery(ProductReviewFilter Filter);

/// <summary>Handles <see cref="GetProductReviewsQuery"/>.</summary>
public sealed class GetProductReviewsQueryHandler
{
    public static async Task<ErrorOr<PagedResponse<ProductReviewResponse>>> HandleAsync(
        GetProductReviewsQuery request,
        IProductReviewRepository reviewRepository,
        CancellationToken ct
    )
    {
        return await reviewRepository.GetPagedAsync(
            new ProductReviewSpecification(request.Filter),
            request.Filter.PageNumber,
            request.Filter.PageSize,
            ct
        );
    }
}
