namespace BuildingBlocks.Application.Extensions;

/// <summary>
///     Helpers for normalising <see cref="DateTime" /> values at the query boundary.
/// </summary>
public static class DateTimeExtensions
{
    /// <summary>
    ///     Returns the value as UTC. Query-string values bound without an offset have
    ///     <see cref="DateTimeKind.Unspecified" />; Npgsql rejects non-UTC <see cref="DateTime" /> parameters
    ///     against <c>timestamp with time zone</c> columns, so callers must normalise before comparing.
    ///     Unspecified kinds are treated as already-UTC; local kinds are converted.
    /// </summary>
    public static DateTime ToUtcKind(this DateTime value) =>
        value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value, DateTimeKind.Utc),
        };
}
