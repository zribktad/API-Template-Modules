namespace SharedKernel.Extensions;

public static class StringExtensions
{
    /// <summary>
    ///     Returns <paramref name="value" /> unchanged if it fits within <paramref name="maxLength" />,
    ///     or truncated to exactly <paramref name="maxLength" /> characters.
    ///     Null is returned as null.
    /// </summary>
    public static string? Truncate(this string? value, int maxLength)
    {
        if (maxLength < 0)
            throw new ArgumentOutOfRangeException(nameof(maxLength), maxLength, "maxLength cannot be negative.");

        return value is { Length: var len } && len > maxLength ? value[..maxLength] : value;
    }
}
