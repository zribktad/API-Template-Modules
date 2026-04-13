using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Identity.Migrations
{
    /// <inheritdoc />
    public partial class CustomRolePartialUniqueIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(name: "IX_CustomRole_TenantId_Name", table: "CustomRole");

            migrationBuilder.CreateIndex(
                name: "IX_CustomRole_Name",
                table: "CustomRole",
                column: "Name",
                unique: true,
                filter: "\"TenantId\" IS NULL"
            );

            migrationBuilder.CreateIndex(
                name: "IX_CustomRole_TenantId_Name",
                table: "CustomRole",
                columns: new[] { "TenantId", "Name" },
                unique: true,
                filter: "\"TenantId\" IS NOT NULL"
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(name: "IX_CustomRole_Name", table: "CustomRole");

            migrationBuilder.DropIndex(name: "IX_CustomRole_TenantId_Name", table: "CustomRole");

            migrationBuilder.CreateIndex(
                name: "IX_CustomRole_TenantId_Name",
                table: "CustomRole",
                columns: new[] { "TenantId", "Name" },
                unique: true
            );
        }
    }
}
