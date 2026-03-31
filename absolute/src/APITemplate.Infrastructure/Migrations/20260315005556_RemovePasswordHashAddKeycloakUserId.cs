using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace APITemplate.Migrations
{
    /// <inheritdoc />
    public partial class RemovePasswordHashAddKeycloakUserId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PasswordHash",
                table: "Users");

            migrationBuilder.AddColumn<string>(
                name: "KeycloakUserId",
                table: "Users",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Users_KeycloakUserId",
                table: "Users",
                column: "KeycloakUserId",
                unique: true,
                filter: "\"KeycloakUserId\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // WARNING: This rollback is schema-only. All PasswordHash data was permanently lost
            // when the Up() migration dropped the column. After rollback, every row in Users
            // will have PasswordHash = "" (empty string), which is not a valid BCrypt hash.
            // DO NOT roll back this migration in production without a full data recovery plan.
            migrationBuilder.DropIndex(
                name: "IX_Users_KeycloakUserId",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "KeycloakUserId",
                table: "Users");

            migrationBuilder.AddColumn<string>(
                name: "PasswordHash",
                table: "Users",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: false,
                defaultValue: "");

            migrationBuilder.Sql("ALTER TABLE \"Users\" ALTER COLUMN \"PasswordHash\" DROP DEFAULT;");
        }
    }
}
