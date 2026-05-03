using ErrorOr;
using ProductCatalog.Features.Category.GetCategoryById;
using ProductCatalog.GraphQL.DataLoaders;
using Wolverine;

namespace ProductCatalog.GraphQL.Types;

/// <summary>
///     Resolver class for fields on <see cref="ProductType" />.
///     Supports loading reviews and categories via batch data loaders.
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

    /// <summary>
    ///     Loads the category for the given <paramref name="product" /> via the data loader.
    /// </summary>
    public async Task<CategoryResponse?> GetCategory(
        [Parent] ProductResponse product,
        CategoryByIdDataLoader loader,
        CancellationToken ct
    )
    {
        if (product.CategoryId is null)
        {
            return null;
        }

        return await loader.LoadAsync(product.CategoryId.Value, ct);
    }
}
