CREATE INDEX "IX_Tenants_Code_Name_Fts" ON identity."Tenants" USING gin (to_tsvector('english', "Code" || ' ' || "Name"));
