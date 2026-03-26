DROP TRIGGER IF EXISTS trg_set_row_version_users ON "Users";
DROP TRIGGER IF EXISTS trg_set_row_version_tenants ON "Tenants";
DROP TRIGGER IF EXISTS trg_set_row_version_product_reviews ON "ProductReviews";
DROP TRIGGER IF EXISTS trg_set_row_version_products ON "Products";
DROP TRIGGER IF EXISTS trg_set_row_version_categories ON "Categories";
DROP FUNCTION IF EXISTS set_row_version();
