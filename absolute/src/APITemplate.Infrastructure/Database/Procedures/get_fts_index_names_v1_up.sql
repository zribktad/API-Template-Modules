CREATE FUNCTION get_fts_index_names()
RETURNS TABLE("Value" TEXT)
LANGUAGE sql STABLE AS $$
    SELECT indexname::TEXT AS "Value"
    FROM pg_indexes
    WHERE schemaname = 'public'
      AND indexdef LIKE '%to_tsvector%';
$$;
