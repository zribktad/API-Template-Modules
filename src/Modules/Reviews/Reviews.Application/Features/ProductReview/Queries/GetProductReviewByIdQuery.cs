using SharedKernel.Application.Errors;
using Reviews.Application.Features.ProductReview.Mappings;
using Reviews.Domain.Interfaces;
using ErrorOr;

namespace Reviews.Application.Features.ProductReview;

/// <summary>Returns a single product review by its unique identifier, or <see langword="null"/> if not found.</summary>
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
        var item = await reviewRepository.GetByIdAsync(request.Id, ct);
        return item is null ? DomainErrors.Reviews.NotFound(request.Id) : item.ToResponse();
    }
}



