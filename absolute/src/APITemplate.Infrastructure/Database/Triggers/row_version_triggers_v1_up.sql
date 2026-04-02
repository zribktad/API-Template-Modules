CREATE OR REPLACE FUNCTION set_row_version()
RETURNS trigger
LANGUAGE plpgsql
AS $$
BEGIN
    NEW."RowVersion" := decode(md5(random()::text || clock_timestamp()::text || pg_current_xact_id()::text), 'hex');
    RETURN NEW;
END;
$$;

UPDATE "Categories"
SET "RowVersion" = decode(md5(random()::text || clock_timestamp()::text || pg_current_xact_id()::text), 'hex')
WHERE "RowVersion" IS NULL;

UPDATE "Products"
SET "RowVersion" = decode(md5(random()::text || clock_timestamp()::text || pg_current_xact_id()::text), 'hex')
WHERE "RowVersion" IS NULL;

UPDATE "ProductReviews"
SET "RowVersion" = decode(md5(random()::text || clock_timestamp()::text || pg_current_xact_id()::text), 'hex')
WHERE "RowVersion" IS NULL;

UPDATE "Tenants"
SET "RowVersion" = decode(md5(random()::text || clock_timestamp()::text || pg_current_xact_id()::text), 'hex')
WHERE "RowVersion" IS NULL;

UPDATE "Users"
SET "RowVersion" = decode(md5(random()::text || clock_timestamp()::text || pg_current_xact_id()::text), 'hex')
WHERE "RowVersion" IS NULL;

DROP TRIGGER IF EXISTS trg_set_row_version_categories ON "Categories";
CREATE TRIGGER trg_set_row_version_categories
BEFORE INSERT OR UPDATE ON "Categories"
FOR EACH ROW
EXECUTE FUNCTION set_row_version();

DROP TRIGGER IF EXISTS trg_set_row_version_products ON "Products";
CREATE TRIGGER trg_set_row_version_products
BEFORE INSERT OR UPDATE ON "Products"
FOR EACH ROW
EXECUTE FUNCTION set_row_version();

DROP TRIGGER IF EXISTS trg_set_row_version_product_reviews ON "ProductReviews";
CREATE TRIGGER trg_set_row_version_product_reviews
BEFORE INSERT OR UPDATE ON "ProductReviews"
FOR EACH ROW
EXECUTE FUNCTION set_row_version();

DROP TRIGGER IF EXISTS trg_set_row_version_tenants ON "Tenants";
CREATE TRIGGER trg_set_row_version_tenants
BEFORE INSERT OR UPDATE ON "Tenants"
FOR EACH ROW
EXECUTE FUNCTION set_row_version();

DROP TRIGGER IF EXISTS trg_set_row_version_users ON "Users";
CREATE TRIGGER trg_set_row_version_users
BEFORE INSERT OR UPDATE ON "Users"
FOR EACH ROW
EXECUTE FUNCTION set_row_version();
