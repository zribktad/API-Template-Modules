namespace ProductCatalog.Domain.Services;

/// <summary>
///     Default <see cref="IProductReferenceValidator" />. Fetches referenced categories and product-data in bulk,
///     then maps missing IDs to per-item <see cref="BatchResultItem" /> failures.
/// </summary>
internal sealed class ProductReferenceValidator(
    ICategoryRepository categoryRepository,
    IProductDataRepository productDataRepository
) : IProductReferenceValidator
{
    public async Task<List<BatchResultItem>> CheckReferencesAsync<T>(
        IReadOnlyList<T> items,
        IReadOnlySet<int> skipIndices,
        CancellationToken ct
    )
        where T : IProductRequest
    {
        // Category (EF Core / PostgreSQL) and product-data (MongoDB) checks use independent
        // connections, so they can safely run in parallel.
        Task<List<BatchResultItem>> categoryTask = CheckCategoryReferencesAsync(
            items,
            skipIndices,
            ct
        );
        Task<List<BatchResultItem>> productDataTask = CheckProductDataReferencesAsync(
            items,
            skipIndices,
            ct
        );
        await Task.WhenAll(categoryTask, productDataTask);
        return BatchFailureMerge.MergeByIndex(categoryTask.Result, productDataTask.Result);
    }

    private async Task<List<BatchResultItem>> CheckCategoryReferencesAsync<T>(
        IReadOnlyList<T> items,
        IReadOnlySet<int> skipIndices,
        CancellationToken ct
    )
        where T : IProductRequest
    {
        HashSet<Guid> allCategoryIds = items
            .Where(item => item.CategoryId.HasValue)
            .Select(item => item.CategoryId!.Value)
            .ToHashSet();

        if (allCategoryIds.Count == 0)
            return [];

        List<Category> existing = await categoryRepository.ListAsync(
            new CategoriesByIdsSpecification(allCategoryIds),
            ct
        );
        allCategoryIds.ExceptWith(existing.Select(c => c.Id));

        if (allCategoryIds.Count == 0)
            return [];

        List<BatchResultItem> failures = new();

        for (int i = 0; i < items.Count; i++)
        {
            if (skipIndices.Contains(i))
                continue;

            Guid? categoryId = items[i].CategoryId;
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

    private async Task<List<BatchResultItem>> CheckProductDataReferencesAsync<T>(
        IReadOnlyList<T> items,
        IReadOnlySet<int> skipIndices,
        CancellationToken ct
    )
        where T : IProductRequest
    {
        Guid[] allProductDataIds = items
            .Where(item => item.ProductDataIds is { Count: > 0 })
            .SelectMany(item => item.ProductDataIds!)
            .Distinct()
            .ToArray();

        if (allProductDataIds.Length == 0)
            return [];

        HashSet<Guid> existingIds = (
            await productDataRepository.GetByIdsAsync(allProductDataIds, ct)
        )
            .Select(pd => pd.Id)
            .ToHashSet();

        HashSet<Guid> missingIds = allProductDataIds
            .Where(id => !existingIds.Contains(id))
            .ToHashSet();

        if (missingIds.Count == 0)
            return [];

        List<BatchResultItem> failures = new();

        for (int i = 0; i < items.Count; i++)
        {
            if (skipIndices.Contains(i))
                continue;

            IReadOnlyCollection<Guid>? pdIds = items[i].ProductDataIds;
            if (pdIds is not { Count: > 0 })
                continue;

            List<Guid> missing = pdIds.Where(id => missingIds.Contains(id)).Distinct().ToList();
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
