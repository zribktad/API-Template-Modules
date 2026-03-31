using APITemplate.Infrastructure.Database;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace APITemplate.Migrations
{
    /// <inheritdoc />
    public partial class AddStoredProceduresForReindexAndEmailClaim : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(SqlResource.Load("Procedures.get_fts_index_names_v1_up.sql"));
            migrationBuilder.Sql(SqlResource.Load("Procedures.get_index_bloat_percent_v1_up.sql"));
            migrationBuilder.Sql(
                SqlResource.Load("Procedures.claim_retryable_failed_emails_v1_up.sql")
            );
            migrationBuilder.Sql(
                SqlResource.Load("Procedures.claim_expired_failed_emails_v1_up.sql")
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                SqlResource.Load("Procedures.claim_expired_failed_emails_v1_down.sql")
            );
            migrationBuilder.Sql(
                SqlResource.Load("Procedures.claim_retryable_failed_emails_v1_down.sql")
            );
            migrationBuilder.Sql(
                SqlResource.Load("Procedures.get_index_bloat_percent_v1_down.sql")
            );
            migrationBuilder.Sql(SqlResource.Load("Procedures.get_fts_index_names_v1_down.sql"));
        }
    }
}
