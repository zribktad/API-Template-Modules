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
            migrationBuilder.AlterDatabase().Annotation("Npgsql:PostgresExtension:pg_trgm", ",,");

            migrationBuilder.CreateIndex(
                name: "IX_Users_NormalizedEmail",
                table: "Users",
                column: "NormalizedEmail"
            );

            migrationBuilder.CreateIndex(
                name: "IX_Users_NormalizedUsername",
                table: "Users",
                column: "NormalizedUsername"
            );

            migrationBuilder
                .CreateIndex(
                    name: "IX_Users_NormalizedUsername_Trgm",
                    table: "Users",
                    column: "NormalizedUsername"
                )
                .Annotation("Npgsql:IndexMethod", "GIN")
                .Annotation("Npgsql:IndexOperators", new[] { "gin_trgm_ops" });

            migrationBuilder.CreateIndex(
                name: "IX_Users_TenantId_NormalizedEmail",
                table: "Users",
                columns: new[] { "TenantId", "NormalizedEmail" },
                unique: true
            );

            migrationBuilder.CreateIndex(
                name: "IX_Users_TenantId_NormalizedUsername",
                table: "Users",
                columns: new[] { "TenantId", "NormalizedUsername" },
                unique: true
            );

            migrationBuilder
                .CreateIndex(
                    name: "IX_Tenants_Code_Name",
                    table: "Tenants",
                    columns: new[] { "Code", "Name" }
                )
                .Annotation("Npgsql:IndexMethod", "GIN")
                .Annotation("Npgsql:TsVectorConfig", "english");

            migrationBuilder.CreateIndex(
                name: "IX_TenantInvitations_TenantId_NormalizedEmail",
                table: "TenantInvitations",
                columns: new[] { "TenantId", "NormalizedEmail" }
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(name: "IX_Users_NormalizedEmail", table: "Users");

            migrationBuilder.DropIndex(name: "IX_Users_NormalizedUsername", table: "Users");

            migrationBuilder.DropIndex(name: "IX_Users_NormalizedUsername_Trgm", table: "Users");

            migrationBuilder.DropIndex(name: "IX_Users_TenantId_NormalizedEmail", table: "Users");

            migrationBuilder.DropIndex(
                name: "IX_Users_TenantId_NormalizedUsername",
                table: "Users"
            );

            migrationBuilder.DropIndex(name: "IX_Tenants_Code_Name", table: "Tenants");

            migrationBuilder.DropIndex(
                name: "IX_TenantInvitations_TenantId_NormalizedEmail",
                table: "TenantInvitations"
            );

            migrationBuilder
                .AlterDatabase()
                .OldAnnotation("Npgsql:PostgresExtension:pg_trgm", ",,");
        }
    }
}
