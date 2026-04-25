CREATE INDEX "IX_Users_NormalizedUsername_Trgm" ON identity."Users" USING gin ("NormalizedUsername" gin_trgm_ops);
