using ErrorOr;
using ProductCatalog.ValueObjects;

namespace ProductCatalog.Domain.Services;

/// <summary>
///     Default <see cref="IProductBatchValidator{T}" />. Aggregates failures from each validation layer into a
///     single <see cref="BatchFailureContext{T}" /> so each index can collect errors from multiple sources before
///     being rejected.
/// </summary>
internal sealed class ProductBatchValidator<T>(
    IProductReferenceValidator referenceValidator,
    IBatchRule<T> itemValidationRule
) : IProductBatchValidator<T>
    where T : IProductRequest
{
    public async Task<ErrorOr<IReadOnlyList<Price>>> ValidateAsync(
        IReadOnlyList<T> items,
        CancellationToken ct,
        params IBatchRule<T>[] additionalRules
    )
    {
        BatchFailureContext<T> context = new(items);

        await context.ApplyRulesAsync(ct, itemValidationRule);
        if (additionalRules.Length > 0)
            await context.ApplyRulesAsync(ct, additionalRules);

        context.AddFailures(
            await referenceValidator.CheckReferencesAsync(items, context.FailedIndices, ct)
        );

        Price[] prices = new Price[items.Count];
        for (int i = 0; i < items.Count; i++)
        {
            if (context.IsFailed(i))
                continue;

            ErrorOr<Price> priceResult = Price.Create(items[i].Price);
            if (priceResult.IsError)
            {
                Guid? id = items[i] is IHasId hasId ? hasId.Id : null;
                context.AddFailure(i, id, priceResult.FirstError.Description);
                continue;
            }

            prices[i] = priceResult.Value;
        }

        if (context.HasFailures)
            return BatchResponseError.From(context.ToFailureResponse());

        return prices;
    }
}
