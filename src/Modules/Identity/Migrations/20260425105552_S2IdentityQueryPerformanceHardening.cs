using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Identity.Migrations
{
    /// <inheritdoc />
    public partial class S2IdentityQueryPerformanceHardening : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(IdentitySqlResource.Load("pg_trgm_v1_up.sql"));
            migrationBuilder.Sql(IdentitySqlResource.Load("idx_users_username_trgm_v1_up.sql"));
            migrationBuilder.Sql(IdentitySqlResource.Load("idx_users_normalized_email_v1_up.sql"));
            migrationBuilder.Sql(
                IdentitySqlResource.Load("idx_users_normalized_username_v1_up.sql")
            );
            migrationBuilder.Sql(IdentitySqlResource.Load("idx_tenants_fts_v1_up.sql"));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(IdentitySqlResource.Load("idx_tenants_fts_v1_down.sql"));
            migrationBuilder.Sql(
                IdentitySqlResource.Load("idx_users_normalized_username_v1_down.sql")
            );
            migrationBuilder.Sql(
                IdentitySqlResource.Load("idx_users_normalized_email_v1_down.sql")
            );
            migrationBuilder.Sql(IdentitySqlResource.Load("idx_users_username_trgm_v1_down.sql"));
            migrationBuilder.Sql(IdentitySqlResource.Load("pg_trgm_v1_down.sql"));
        }
    }
}
