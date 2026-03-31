CREATE FUNCTION get_product_category_stats(p_category_id UUID, p_tenant_id UUID)
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
    LEFT JOIN "Products"       p  ON p."CategoryId" = c."Id" AND p."TenantId" = p_tenant_id AND NOT p."IsDeleted"
    LEFT JOIN "ProductReviews" pr ON pr."ProductId" = p."Id" AND pr."TenantId" = p_tenant_id AND NOT pr."IsDeleted"
    WHERE c."Id" = p_category_id
      AND c."TenantId" = p_tenant_id
      AND NOT c."IsDeleted"
    GROUP BY c."Id", c."Name";
END;
$$;
