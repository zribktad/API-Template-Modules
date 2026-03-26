namespace APITemplate.Infrastructure.StoredProcedures;

/// <summary>
/// Calls the <c>get_fts_index_names()</c> PostgreSQL function.
/// Returns full-text search index names from the public schema.
///
/// Result: single <c>text</c> column aliased as <c>"Value"</c>.
/// Used via <c>Database.SqlQuery&lt;string&gt;</c> (primitive return type).
/// </summary>
public sealed record GetFtsIndexNamesProcedure
{
    public FormattableString ToSql() => $"SELECT * FROM get_fts_index_names()";
}
