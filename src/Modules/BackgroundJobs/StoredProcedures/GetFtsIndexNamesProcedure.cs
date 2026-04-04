namespace BackgroundJobs.StoredProcedures;

/// <summary>
///     Calls the <c>get_fts_index_names()</c> PostgreSQL function.
///     Returns the names of all full-text search indexes in the <c>public</c> schema
///     (indexes whose definition contains <c>to_tsvector</c>).
/// </summary>
public sealed record GetFtsIndexNamesProcedure : IScalarStoredProcedure<string>
{
    public FormattableString ToSql()
    {
        return $"SELECT * FROM get_fts_index_names()";
    }
}
