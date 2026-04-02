namespace APITemplate.Application.Common.DTOs;

/// <summary>
/// Summarises the outcome of a batch operation, including per-item failure details and aggregate counts.
/// </summary>
public sealed record BatchResponse(
    IReadOnlyList<BatchResultItem> Failures,
    int SuccessCount,
    int FailureCount
);

/// <summary>
/// Represents a failed item within a batch operation, including its zero-based index,
/// the affected entity ID (when known), and validation/existence errors.
/// </summary>
public sealed record BatchResultItem(int Index, Guid? Id, IReadOnlyList<string> Errors);
