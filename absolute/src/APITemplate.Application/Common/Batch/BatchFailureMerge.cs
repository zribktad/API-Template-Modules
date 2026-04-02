using APITemplate.Application.Common.DTOs;

namespace APITemplate.Application.Common.Batch;

/// <summary>
/// Merges per-item batch failures that share the same index (e.g. missing category and missing product data).
/// </summary>
internal static class BatchFailureMerge
{
    internal static List<BatchResultItem> MergeByIndex(
        IEnumerable<BatchResultItem> first,
        IEnumerable<BatchResultItem> second
    )
    {
        var errorsByIndex = new Dictionary<int, List<string>>();
        var idByIndex = new Dictionary<int, Guid?>();

        void Accumulate(BatchResultItem item)
        {
            if (!errorsByIndex.TryGetValue(item.Index, out var list))
            {
                list = [];
                errorsByIndex[item.Index] = list;
            }

            list.AddRange(item.Errors);

            if (!idByIndex.TryGetValue(item.Index, out var existingId))
                idByIndex[item.Index] = item.Id;
            else if (existingId is null && item.Id is not null)
                idByIndex[item.Index] = item.Id;
        }

        foreach (var x in first)
            Accumulate(x);
        foreach (var x in second)
            Accumulate(x);

        return errorsByIndex
            .OrderBy(kv => kv.Key)
            .Select(kv => new BatchResultItem(kv.Key, idByIndex[kv.Key], kv.Value))
            .ToList();
    }
}
