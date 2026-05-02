using ErrorOr;
using SharedKernel.Contracts.Queries.Reviews;

namespace Reviews.Features;

/// <summary>Handles <see cref="GetProductReviewsByProductIdsQuery" />.</summary>
public sealed class GetProductReviewsByProductIdsQueryHandler
{
    public static async Task<
        ErrorOr<IReadOnlyDictionary<Guid, ProductReviewResponse[]>>
    > HandleAsync(
        GetProductReviewsByProductIdsQuery request,
        IProductReviewRepository reviewRepository,
        CancellationToken ct
    )
    {
        if (request.ProductIds.Count == 0)
        {
            return (ErrorOr<IReadOnlyDictionary<Guid, ProductReviewResponse[]>>)
                new Dictionary<Guid, ProductReviewResponse[]>();
        }

        List<ProductReviewResponse> reviews = await reviewRepository.ListAsync(
            new ProductReviewByProductIdsSpecification(request.ProductIds),
            ct
        );
        ILookup<Guid, ProductReviewResponse> lookup = reviews.ToLookup(review => review.ProductId);

        return (ErrorOr<IReadOnlyDictionary<Guid, ProductReviewResponse[]>>)
            request.ProductIds.Distinct().ToDictionary(id => id, id => lookup[id].ToArray());
    }
}
