namespace BackgroundJobs.StoredProcedures;

public sealed record GetFtsIndexNamesProcedure
{
    public FormattableString ToSql() => $"SELECT * FROM get_fts_index_names()";
}
