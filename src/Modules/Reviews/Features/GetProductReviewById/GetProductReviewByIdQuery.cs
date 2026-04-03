using ErrorOr;
using Reviews.Domain;

namespace Reviews.Features;

/// <summary>Returns a single product review by its unique identifier, or a not-found error if it does not exist.</summary>
public sealed record GetProductReviewByIdQuery(Guid Id) : IHasId;

/// <summary>Handles <see cref="GetProductReviewByIdQuery"/>.</summary>
public sealed class GetProductReviewByIdQueryHandler
{
    public static async Task<ErrorOr<ProductReviewResponse>> HandleAsync(
        GetProductReviewByIdQuery request,
        IProductReviewRepository reviewRepository,
        CancellationToken ct
    )
    {
        Domain.Entities.ProductReview? item = await reviewRepository.GetByIdAsync(request.Id, ct);
        return item is null ? DomainErrors.Reviews.NotFound(request.Id) : item.ToResponse();
    }
}







