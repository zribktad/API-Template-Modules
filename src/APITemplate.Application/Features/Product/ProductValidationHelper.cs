using APITemplate.Application.Common.Batch;

namespace APITemplate.Application.Features.Product;

/// <summary>Shared validation methods for product commands.</summary>
internal static class ProductValidationHelper
{
    /// <summary>
    /// Checks all product references (category and product data) in a single call, merging
    /// per-item failures from both checks. Items in <paramref name="failedIndices"/> are skipped.
    /// </summary>
    internal static async Task<List<BatchResultItem>> CheckProductReferencesAsync<T>(
        IReadOnlyList<T> items,
        ICategoryRepository categoryRepository,
        IProductDataRepository productDataRepository,
        IReadOnlySet<int> failedIndices,
        CancellationToken ct
    )
        where T : IProductRequest
    {
        // Category (EF Core / PostgreSQL) and product-data (MongoDB) checks use independent
        // connections, so they can safely run in parallel.
        Task<List<BatchResultItem>> categoryTask = CheckCategoryReferencesAsync(
            items,
            item => item.CategoryId,
            categoryRepository,
            failedIndices,
            ct
        );
        Task<List<BatchResultItem>> productDataTask = CheckProductDataReferencesAsync(
            items,
            item => item.ProductDataIds,
            productDataRepository,
            failedIndices,
            ct
        );
        await Task.WhenAll(categoryTask, productDataTask);
        return BatchFailureMerge.MergeByIndex(categoryTask.Result, productDataTask.Result);
    }

    /// <summary>
    /// Checks that all referenced category IDs exist and returns per-item failures for items
    /// that reference a missing category. Items in <paramref name="failedIndices"/> are skipped.
    /// </summary>
    internal static async Task<List<BatchResultItem>> CheckCategoryReferencesAsync<T>(
        IReadOnlyList<T> items,
        Func<T, Guid?> categoryIdSelector,
        ICategoryRepository categoryRepository,
        IReadOnlySet<int> failedIndices,
        CancellationToken ct
    )
    {
        var allCategoryIds = items
            .Where(item => categoryIdSelector(item).HasValue)
            .Select(item => categoryIdSelector(item)!.Value)
            .ToHashSet();

        if (allCategoryIds.Count == 0)
            return [];

        var existing = await categoryRepository.ListAsync(
            new Category.Specifications.CategoriesByIdsSpecification(allCategoryIds),
            ct
        );
        allCategoryIds.ExceptWith(existing.Select(c => c.Id));

        if (allCategoryIds.Count == 0)
            return [];

        var failures = new List<BatchResultItem>();

        for (var i = 0; i < items.Count; i++)
        {
            if (failedIndices.Contains(i))
                continue;

            var categoryId = categoryIdSelector(items[i]);
            if (categoryId.HasValue && allCategoryIds.Contains(categoryId.Value))
            {
                Guid? failureId = items[i] is IHasId hasId ? hasId.Id : null;
                failures.Add(
                    new BatchResultItem(
                        i,
                        failureId,
                        [string.Format(ErrorCatalog.Categories.NotFoundMessage, categoryId)]
                    )
                );
            }
        }

        return failures;
    }

    /// <summary>
    /// Checks that all referenced product-data IDs exist and returns per-item failures for items
    /// that reference missing product data. Items in <paramref name="failedIndices"/> are skipped.
    /// </summary>
    internal static async Task<List<BatchResultItem>> CheckProductDataReferencesAsync<T>(
        IReadOnlyList<T> items,
        Func<T, IReadOnlyCollection<Guid>?> productDataIdsSelector,
        IProductDataRepository productDataRepository,
        IReadOnlySet<int> failedIndices,
        CancellationToken ct
    )
    {
        var allProductDataIds = items
            .Where(item => productDataIdsSelector(item) is { Count: > 0 })
            .SelectMany(item => productDataIdsSelector(item)!)
            .Distinct()
            .ToArray();

        if (allProductDataIds.Length == 0)
            return [];

        var existingIds = (await productDataRepository.GetByIdsAsync(allProductDataIds, ct))
            .Select(pd => pd.Id)
            .ToHashSet();

        var missingIds = allProductDataIds.Where(id => !existingIds.Contains(id)).ToHashSet();

        if (missingIds.Count == 0)
            return [];

        var failures = new List<BatchResultItem>();

        for (var i = 0; i < items.Count; i++)
        {
            if (failedIndices.Contains(i))
                continue;

            var pdIds = productDataIdsSelector(items[i]);
            if (pdIds is not { Count: > 0 })
                continue;

            var missing = pdIds.Where(id => missingIds.Contains(id)).ToList();
            if (missing.Count > 0)
            {
                Guid? failureId = items[i] is IHasId hasId ? hasId.Id : null;
                failures.Add(
                    new BatchResultItem(
                        i,
                        failureId,
                        [
                            string.Format(
                                ErrorCatalog.ProductData.NotFoundMessage,
                                string.Join(", ", missing)
                            ),
                        ]
                    )
                );
            }
        }

        return failures;
    }
}
