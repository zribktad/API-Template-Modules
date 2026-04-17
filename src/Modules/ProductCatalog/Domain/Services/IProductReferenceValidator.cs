namespace ProductCatalog.Domain.Services;

/// <summary>
///     Validates category and product-data references carried by product requests.
///     <para>
///         Produces per-item <see cref="BatchResultItem" /> failures that the caller can merge into a
///         <see cref="BatchFailureContext{T}" />. Items whose index is present in the <c>skipIndices</c> set are
///         ignored, allowing earlier validation layers (fluent rules, uniqueness checks) to short-circuit them.
///     </para>
///     <para>
///         Repository lookups on category (PostgreSQL) and product-data (MongoDB) run in parallel.
///     </para>
/// </summary>
public interface IProductReferenceValidator
{
    /// <summary>
    ///     Runs category and product-data existence checks against <paramref name="items" /> and returns a merged
    ///     list of per-item failures. An empty result means every reference resolves.
    /// </summary>
    Task<List<BatchResultItem>> CheckReferencesAsync<T>(
        IReadOnlyList<T> items,
        IReadOnlySet<int> skipIndices,
        CancellationToken ct
    )
        where T : IProductRequest;
}
