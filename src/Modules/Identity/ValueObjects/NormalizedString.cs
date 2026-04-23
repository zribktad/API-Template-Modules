namespace Identity.ValueObjects;

/// <summary>
///     Pairs the original user-supplied string with its trimmed, upper-case invariant form.
///     Both forms are persisted so display output is never mutated while database indexes and
///     uniqueness checks always operate on a stable, culture-neutral representation — preventing
///     silent duplicates such as "Alice", " alice " and "ALICE" from being treated as distinct.
///     Use <see cref="Value"/> for output; use <see cref="Normalized"/> for filtering and index lookups.
/// </summary>
public sealed class NormalizedString
{
    public string Value { get; private set; } = string.Empty;
    public string Normalized { get; private set; } = string.Empty;

    private NormalizedString() { }

    public NormalizedString(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        Value = value.Trim();
        if (Value.Length == 0)
            throw new ArgumentException("Value cannot be empty or whitespace.", nameof(value));
        Normalized = Value.ToUpperInvariant();
    }

    public static string Normalize(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("Value cannot be empty or whitespace.", nameof(value));
        return value.Trim().ToUpperInvariant();
    }
}
