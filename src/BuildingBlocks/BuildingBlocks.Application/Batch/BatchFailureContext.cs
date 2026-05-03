using BuildingBlocks.Application.DTOs;

namespace BuildingBlocks.Application.Batch;

/// <summary>
///     Holds batch items and collects per-item failures across validation rules.
/// </summary>
public sealed class BatchFailureContext<TItem>
{
    private readonly HashSet<int> _failedIndices = [];
    private readonly List<BatchResultItem> _failures = [];

    public BatchFailureContext(IReadOnlyList<TItem> items)
    {
        Items = items;
    }

    public IReadOnlyList<TItem> Items { get; }
    public bool HasFailures => _failures.Count > 0;
    public IReadOnlySet<int> FailedIndices => _failedIndices;

    public void AddFailure(int index, Guid? id, IReadOnlyList<string> errors)
    {
        _failures.Add(new BatchResultItem(index, id, errors));
        _failedIndices.Add(index);
    }

    public void AddFailure(int index, Guid? id, string error)
    {
        AddFailure(index, id, [error]);
    }

    public void AddFailures(IEnumerable<BatchResultItem> failures)
    {
        foreach (BatchResultItem failure in failures)
        {
            _failures.Add(failure);
            _failedIndices.Add(failure.Index);
        }
    }

    public bool IsFailed(int index)
    {
        return _failedIndices.Contains(index);
    }

    public async Task ApplyRulesAsync(CancellationToken ct, params IBatchRule<TItem>[] rules)
    {
        for (int i = 0; i < rules.Length; i++)
            await rules[i].ApplyAsync(this, ct);
    }

    public BatchResponse ToFailureResponse()
    {
        return new BatchResponse(_failures, 0, _failures.Count);
    }
}

