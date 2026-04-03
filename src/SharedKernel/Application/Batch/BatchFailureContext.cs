using SharedKernel.Application.DTOs;

namespace SharedKernel.Application.Batch;

/// <summary>
/// Holds batch items and collects per-item failures across validation rules.
/// </summary>
public sealed class BatchFailureContext<TItem>
{
    private readonly List<BatchResultItem> _failures = [];
    private readonly HashSet<int> _failedIndices = [];

    public BatchFailureContext(IReadOnlyList<TItem> items) => Items = items;

    public IReadOnlyList<TItem> Items { get; }
    public bool HasFailures => _failures.Count > 0;
    public IReadOnlySet<int> FailedIndices => _failedIndices;

    public void AddFailure(int index, Guid? id, IReadOnlyList<string> errors)
    {
        _failures.Add(new BatchResultItem(index, id, errors));
        _failedIndices.Add(index);
    }

    public void AddFailure(int index, Guid? id, string error) => AddFailure(index, id, [error]);

    public void AddFailures(IEnumerable<BatchResultItem> failures)
    {
        foreach (BatchResultItem failure in failures)
        {
            _failures.Add(failure);
            _failedIndices.Add(failure.Index);
        }
    }

    public bool IsFailed(int index) => _failedIndices.Contains(index);

    public async Task ApplyRulesAsync(CancellationToken ct, params IBatchRule<TItem>[] rules)
    {
        for (var i = 0; i < rules.Length; i++)
            await rules[i].ApplyAsync(this, ct);
    }

    public BatchResponse ToFailureResponse() => new(_failures, 0, _failures.Count);
}
