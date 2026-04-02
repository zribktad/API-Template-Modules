namespace APITemplate.Application.Common.Batch;

/// <summary>
/// Holds batch items and collects per-item failures across validation rules.
/// </summary>
internal sealed class BatchFailureContext<TItem>
{
    private readonly List<BatchResultItem> _failures = [];
    private readonly HashSet<int> _failedIndices = [];

    internal BatchFailureContext(IReadOnlyList<TItem> items) => Items = items;

    internal IReadOnlyList<TItem> Items { get; }
    internal bool HasFailures => _failures.Count > 0;
    internal IReadOnlySet<int> FailedIndices => _failedIndices;

    internal void AddFailure(int index, Guid? id, IReadOnlyList<string> errors)
    {
        _failures.Add(new BatchResultItem(index, id, errors));
        _failedIndices.Add(index);
    }

    internal void AddFailure(int index, Guid? id, string error) => AddFailure(index, id, [error]);

    internal void AddFailures(IEnumerable<BatchResultItem> failures)
    {
        foreach (var failure in failures)
        {
            _failures.Add(failure);
            _failedIndices.Add(failure.Index);
        }
    }

    internal bool IsFailed(int index) => _failedIndices.Contains(index);

    internal async Task ApplyRulesAsync(CancellationToken ct, params IBatchRule<TItem>[] rules)
    {
        for (var i = 0; i < rules.Length; i++)
            await rules[i].ApplyAsync(this, ct);
    }

    internal BatchResponse ToFailureResponse() => new(_failures, 0, _failures.Count);
}
