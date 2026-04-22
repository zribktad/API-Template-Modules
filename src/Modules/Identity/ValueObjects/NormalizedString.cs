namespace Identity.ValueObjects;

/// <summary>
/// Value object that pairs a user-supplied string with its normalised form used for
/// case-insensitive uniqueness checks and lookups.
///
/// Normalisation rule: trim surrounding whitespace, then convert to upper-case
/// (invariant culture).  Storing both forms means the display value is never
/// mutated while queries and unique indexes always operate on a predictable,
/// culture-neutral representation — eliminating silent duplicates such as
/// "Alice", " alice " and "ALICE" being treated as distinct identities.
///
/// Use <see cref="Value"/> when displaying or returning data to callers.
/// Use <see cref="Normalized"/> (or <see cref="Normalize"/>) when filtering,
/// comparing, or building unique indexes in the database.
/// </summary>
public sealed record NormalizedString
{
    public string Value { get; private init; } = string.Empty;
    public string Normalized { get; private init; } = string.Empty;

    private NormalizedString() { }

    public NormalizedString(string value)
    {
        Value = value.Trim();
        Normalized = Value.ToUpperInvariant();
    }

    public static string Normalize(string value) => value.Trim().ToUpperInvariant();
}
