namespace SharedKernel.Application.Sorting;

/// <summary>
/// Represents a named, case-insensitive sort field that can be compared against a raw string value
/// supplied by an API caller.
/// </summary>
public sealed record SortField(string Value)
{
    /// <summary>
    /// Returns <see langword="true"/> when <paramref name="input"/> (after trimming) matches
    /// this field's <see cref="Value"/> using a case-insensitive ordinal comparison.
    /// </summary>
    public bool Matches(string? input) =>
        string.Equals(Value, input?.Trim(), StringComparison.OrdinalIgnoreCase);
}
