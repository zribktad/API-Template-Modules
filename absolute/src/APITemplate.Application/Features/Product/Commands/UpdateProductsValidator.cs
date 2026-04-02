using APITemplate.Application.Common.Batch;
using APITemplate.Application.Common.Batch.Rules;
using APITemplate.Application.Features.Product.Specifications;
using FluentValidation;
using ProductEntity = APITemplate.Domain.Entities.Product;

namespace APITemplate.Application.Features.Product;

/// <summary>
/// Validates all items in an <see cref="UpdateProductsCommand"/> and loads target products.
/// Returns a failure <see cref="BatchResponse"/> when any rule fails, or <c>null</c> on the
/// happy path together with the loaded product map.
/// </summary>
internal static class UpdateProductsValidator
{
    internal static async Task<(
        BatchResponse? Failure,
        Dictionary<Guid, ProductEntity>? ProductMap
    )> ValidateAndLoadAsync(
        UpdateProductsCommand command,
        IProductRepository repository,
        ICategoryRepository categoryRepository,
        IProductDataRepository productDataRepository,
        IValidator<UpdateProductItem> itemValidator,
        CancellationToken ct
    )
    {
        IReadOnlyList<UpdateProductItem> items = command.Request.Items;
        BatchFailureContext<UpdateProductItem> context = new(items);

        // Validate each item (field-level rules — name, price, etc.)
        await context.ApplyRulesAsync(
            ct,
            new FluentValidationBatchRule<UpdateProductItem>(itemValidator)
        );

        // Load all target products and mark missing ones as failed
        HashSet<Guid> requestedIds = items
            .Where((_, i) => !context.IsFailed(i))
            .Select(item => item.Id)
            .ToHashSet();
        Dictionary<Guid, ProductEntity> productMap = (
            await repository.ListAsync(new ProductsByIdsWithLinksSpecification(requestedIds), ct)
        ).ToDictionary(p => p.Id);

        await context.ApplyRulesAsync(
            ct,
            new MarkMissingByIdBatchRule<UpdateProductItem>(
                item => item.Id,
                productMap.Keys.ToHashSet(),
                ErrorCatalog.Products.NotFoundMessage
            )
        );

        // Reference checks skip only earlier failures (validation + missing entity) so
        // category and product-data issues on the same row are merged into one failure.
        context.AddFailures(
            await ProductValidationHelper.CheckProductReferencesAsync(
                items,
                categoryRepository,
                productDataRepository,
                context.FailedIndices,
                ct
            )
        );

        if (context.HasFailures)
            return (context.ToFailureResponse(), null);

        return (null, productMap);
    }
}
