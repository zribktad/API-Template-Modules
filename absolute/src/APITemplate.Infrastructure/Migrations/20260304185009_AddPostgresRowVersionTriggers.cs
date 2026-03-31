using APITemplate.Infrastructure.Database;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace APITemplate.Migrations
{
    /// <inheritdoc />
    public partial class AddPostgresRowVersionTriggers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(SqlResource.Load("Triggers.row_version_triggers_v1_up.sql"));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(SqlResource.Load("Triggers.row_version_triggers_v1_down.sql"));
        }
    }
}
