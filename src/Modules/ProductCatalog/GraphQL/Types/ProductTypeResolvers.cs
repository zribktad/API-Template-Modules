using ProductCatalog.GraphQL.DataLoaders;

namespace ProductCatalog.GraphQL.Types;

/// <summary>
///     Resolver class for the <c>reviews</c> field on <see cref="ProductType" />.
///     Delegates to <see cref="ProductReviewsByProductDataLoader" /> to batch-load reviews and
///     returns an empty array when no reviews exist for the product.
/// </summary>
public sealed class ProductTypeResolvers
{
    /// <summary>
    ///     Loads reviews for the given <paramref name="product" /> via the batch data loader,
    ///     returning an empty array when the loader yields no result.
    /// </summary>
    public async Task<ProductReviewResponse[]> GetReviews(
        [Parent] ProductResponse product,
        ProductReviewsByProductDataLoader loader,
        CancellationToken ct
    )
    {
        return await loader.LoadAsync(product.Id, ct) ?? Array.Empty<ProductReviewResponse>();
    }
}
