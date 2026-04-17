using ErrorOr;
using ProductCatalog.Features.Product.CreateProducts;
using ProductEntity = ProductCatalog.Entities.Product;

namespace ProductCatalog.Domain.Services;

/// <summary>
///     Constructs the <see cref="ProductEntity" /> aggregates for a batch-create request after delegating
///     validation (fluent rules, reference checks, <c>Price</c> invariants) to
///     <see cref="IProductBatchValidator{T}" />.
///     <para>Keeps handlers thin — the handler only persists entities and publishes cache invalidations.</para>
/// </summary>
public interface IProductBatchFactory
{
    /// <summary>
    ///     Validates and constructs entities for <paramref name="items" />. On validation failure returns an
    ///     <see cref="ErrorOr{TValue}" /> error wrapping the per-item <see cref="BatchResponse" /> via
    ///     <see cref="BatchResponseError" />.
    /// </summary>
    Task<ErrorOr<IReadOnlyList<ProductEntity>>> CreateAsync(
        IReadOnlyList<CreateProductRequest> items,
        CancellationToken ct
    );
}
