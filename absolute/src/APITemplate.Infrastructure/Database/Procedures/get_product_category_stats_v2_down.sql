DROP FUNCTION IF EXISTS get_product_category_stats(UUID, UUID);
CREATE FUNCTION get_product_category_stats(p_category_id UUID)
RETURNS TABLE(
    "CategoryId"   UUID,
    "CategoryName" TEXT,
    "ProductCount" BIGINT,
    "AveragePrice" NUMERIC,
    "TotalReviews" BIGINT
)
LANGUAGE plpgsql AS $$
BEGIN
    RETURN QUERY
    SELECT
        c."Id"                      AS "CategoryId",
        c."Name"::TEXT              AS "CategoryName",
        COUNT(DISTINCT p."Id")      AS "ProductCount",
        COALESCE(AVG(p."Price"), 0) AS "AveragePrice",
        COUNT(pr."Id")              AS "TotalReviews"
    FROM "Categories" c
    LEFT JOIN "Products"       p  ON p."CategoryId" = c."Id"
    LEFT JOIN "ProductReviews" pr ON pr."ProductId"  = p."Id"
    WHERE c."Id" = p_category_id
    GROUP BY c."Id", c."Name";
END;
$$;
