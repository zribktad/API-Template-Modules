using BuildingBlocks.Application.DTOs;

namespace BuildingBlocks.Application.Batch;

/// <summary>
///     Merges per-item batch failures that share the same index (e.g. missing category and missing product data).
/// </summary>
public static class BatchFailureMerge
{
    public static List<BatchResultItem> MergeByIndex(
        IEnumerable<BatchResultItem> first,
        IEnumerable<BatchResultItem> second
    )
    {
        Dictionary<int, List<string>> errorsByIndex = new();
        Dictionary<int, Guid?> idByIndex = new();

        void Accumulate(BatchResultItem item)
        {
            if (!errorsByIndex.TryGetValue(item.Index, out List<string>? list))
            {
                list = [];
                errorsByIndex[item.Index] = list;
            }

            list.AddRange(item.Errors);

            if (!idByIndex.TryGetValue(item.Index, out Guid? existingId))
                idByIndex[item.Index] = item.Id;
            else if (existingId is null && item.Id is not null)
                idByIndex[item.Index] = item.Id;
        }

        foreach (BatchResultItem x in first)
            Accumulate(x);
        foreach (BatchResultItem x in second)
            Accumulate(x);

        return errorsByIndex
            .OrderBy(kv => kv.Key)
            .Select(kv => new BatchResultItem(kv.Key, idByIndex[kv.Key], kv.Value))
            .ToList();
    }
}

