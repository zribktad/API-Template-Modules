using ProductCatalog.Features.Product.CreateProducts;
using ProductEntity = ProductCatalog.Entities.Product;

namespace ProductCatalog.Domain.Services;

/// <summary>
///     Result of a product batch creation attempt.
///     <para>
///         On success, <see cref="Entities" /> holds the fully constructed (not-yet-persisted) products and
///         <see cref="Failure" /> is <c>null</c>. On failure, <see cref="Failure" /> carries a populated
///         <see cref="BatchResponse" /> describing per-item errors and <see cref="Entities" /> is <c>null</c>.
///     </para>
/// </summary>
public sealed record ProductBatchCreateResult(
    IReadOnlyList<ProductEntity>? Entities,
    BatchResponse? Failure
);

/// <summary>
///     Orchestrates the full batch-create workflow for products: applies fluent item rules, validates references
///     through <see cref="IProductReferenceValidator" />, enforces <c>Price</c> value-object invariants, and builds
///     the resulting <see cref="ProductEntity" /> aggregates.
///     <para>Keeps handlers thin — the handler only persists entities and publishes cache invalidations.</para>
/// </summary>
public interface IProductBatchFactory
{
    /// <summary>
    ///     Validates and constructs the entities for <paramref name="items" />. Never throws for domain-level
    ///     failures — returns them via <see cref="ProductBatchCreateResult.Failure" /> instead.
    /// </summary>
    Task<ProductBatchCreateResult> CreateAsync(
        IReadOnlyList<CreateProductRequest> items,
        CancellationToken ct
    );
}
