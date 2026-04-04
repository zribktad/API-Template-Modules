namespace BackgroundJobs.StoredProcedures;

/// <summary>
///     Calls the <c>get_index_bloat_percent(p_index_name)</c> PostgreSQL function.
///     Returns the estimated bloat percentage of the given index, calculated from
///     <c>pg_class</c> / <c>pg_stat_user_indexes</c> catalog statistics.
/// </summary>
public sealed record GetIndexBloatPercentProcedure(string IndexName)
    : IScalarStoredProcedure<double>
{
    public FormattableString ToSql()
    {
        return $"SELECT * FROM get_index_bloat_percent({IndexName})";
    }
}
