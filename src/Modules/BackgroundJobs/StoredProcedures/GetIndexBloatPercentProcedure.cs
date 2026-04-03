namespace BackgroundJobs.StoredProcedures;

public sealed record GetIndexBloatPercentProcedure(string IndexName)
{
    public FormattableString ToSql() => $"SELECT * FROM get_index_bloat_percent({IndexName})";
}



