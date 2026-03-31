using System.Data;

namespace SharedKernel.Domain.Options;

/// <summary>
/// Per-call overrides for the transaction policy applied by <see cref="Interfaces.IUnitOfWork.ExecuteInTransactionAsync(Func{Task}, CancellationToken, TransactionOptions?)"/>.
/// Any <c>null</c> property means "inherit the configured default"; non-null values override that default for the outermost transaction only.
/// </summary>
public sealed record TransactionOptions
{
    public IsolationLevel? IsolationLevel { get; init; }
    public int? TimeoutSeconds { get; init; }
    public bool? RetryEnabled { get; init; }
    public int? RetryCount { get; init; }
    public int? RetryDelaySeconds { get; init; }

    /// <summary>
    /// Returns <c>true</c> when all properties are <c>null</c>, meaning the record carries no overrides
    /// and the configured defaults apply entirely.
    /// </summary>
    public bool IsEmpty() =>
        IsolationLevel is null
        && TimeoutSeconds is null
        && RetryEnabled is null
        && RetryCount is null
        && RetryDelaySeconds is null;
}
