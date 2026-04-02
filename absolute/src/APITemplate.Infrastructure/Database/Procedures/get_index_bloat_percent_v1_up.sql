CREATE FUNCTION get_index_bloat_percent(p_index_name TEXT)
RETURNS TABLE("Value" DOUBLE PRECISION)
LANGUAGE sql STABLE AS $$
    SELECT CASE
        WHEN pg_relation_size(c.oid) = 0 THEN 0
        ELSE GREATEST(0,
            100.0 * (1.0 - (
                (s.n_live_tup::float * COALESCE(NULLIF(avg_w.avg_width, 0), 32) / 0.9)
                / NULLIF(pg_relation_size(c.oid)::float, 0)
            ))
        )
    END AS "Value"
    FROM pg_class c
    JOIN pg_stat_user_indexes si ON si.indexrelid = c.oid
    JOIN pg_stat_user_tables s ON s.relid = si.relid
    CROSS JOIN LATERAL (
        SELECT COALESCE(SUM(ps.avg_width), 32)::int AS avg_width
        FROM pg_stats ps
        WHERE ps.schemaname = 'public'
          AND ps.tablename = s.relname
    ) avg_w
    WHERE c.relname = p_index_name;
$$;
