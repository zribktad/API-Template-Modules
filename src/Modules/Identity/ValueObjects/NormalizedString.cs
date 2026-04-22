namespace Identity.ValueObjects;

/// <summary>
///     Pairs the original user-supplied string with its trimmed, upper-case invariant form.
///     Both forms are persisted so display output is never mutated while database indexes and
///     uniqueness checks always operate on a stable, culture-neutral representation — preventing
///     silent duplicates such as "Alice", " alice " and "ALICE" from being treated as distinct.
///     Use <see cref="Value"/> for output; use <see cref="Normalized"/> for filtering and index lookups.
/// </summary>
public sealed record NormalizedString
{
    public string Value { get; private init; } = string.Empty;
    public string Normalized { get; private init; } = string.Empty;

    private NormalizedString() { }

    public NormalizedString(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        Value = value.Trim();
        Normalized = Value.ToUpperInvariant();
    }

    public static string Normalize(string value) => value.Trim().ToUpperInvariant();
}
