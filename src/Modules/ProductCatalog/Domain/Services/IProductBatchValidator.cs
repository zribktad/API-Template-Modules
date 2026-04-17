using ErrorOr;
using ProductCatalog.ValueObjects;

namespace ProductCatalog.Domain.Services;

/// <summary>
///     Runs the shared product-batch validation pipeline (fluent rule → caller-supplied rules → reference checks
///     → <see cref="Price" /> value-object) for any item type that exposes <see cref="IProductRequest" />.
///     <para>
///         On success returns the validated <see cref="Price" /> for each item (indexed parallel to <c>items</c>).
///         On failure returns an <see cref="ErrorOr{TValue}" /> error wrapping a <see cref="BatchResponse" /> via
///         <see cref="BatchResponseError" /> so the caller can forward per-item failures as the response payload.
///     </para>
/// </summary>
public interface IProductBatchValidator<T>
    where T : IProductRequest
{
    Task<ErrorOr<IReadOnlyList<Price>>> ValidateAsync(
        IReadOnlyList<T> items,
        CancellationToken ct,
        params IBatchRule<T>[] additionalRules
    );
}
