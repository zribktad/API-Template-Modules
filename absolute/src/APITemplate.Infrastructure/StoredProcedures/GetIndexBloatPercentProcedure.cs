namespace APITemplate.Infrastructure.StoredProcedures;

/// <summary>
/// Calls the <c>get_index_bloat_percent(p_index_name)</c> PostgreSQL function.
/// Estimates index bloat by comparing actual size to ideal size derived from
/// live table rows and average row width.
///
/// Result: single <c>double precision</c> column aliased as <c>"Value"</c>.
/// Used via <c>Database.SqlQuery&lt;double&gt;</c> (primitive return type).
/// </summary>
public sealed record GetIndexBloatPercentProcedure(string IndexName)
{
    public FormattableString ToSql() => $"SELECT * FROM get_index_bloat_percent({IndexName})";
}
