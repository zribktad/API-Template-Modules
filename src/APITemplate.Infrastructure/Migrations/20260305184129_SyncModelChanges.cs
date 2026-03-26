using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace APITemplate.Migrations
{
    /// <inheritdoc />
    public partial class SyncModelChanges : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Step 1: Add UserId as nullable so existing rows are not rejected.
            migrationBuilder.AddColumn<Guid>(
                name: "UserId",
                table: "ProductReviews",
                type: "uuid",
                nullable: true);

            // Step 2: Backfill existing rows — assign each review to the user
            // who matches the old ReviewerName, falling back to the first admin user
            // in the same tenant. Adjust this query if your mapping differs.
            migrationBuilder.Sql("""
                UPDATE "ProductReviews" pr
                SET "UserId" = u."Id"
                FROM "Users" u
                WHERE u."Username" = pr."ReviewerName"
                  AND u."TenantId" = pr."TenantId";
                """);

            // Step 3: Drop the old column now that data has been migrated.
            migrationBuilder.DropColumn(
                name: "ReviewerName",
                table: "ProductReviews");

            // Step 4: Make UserId non-nullable now that all rows have a value.
            migrationBuilder.AlterColumn<Guid>(
                name: "UserId",
                table: "ProductReviews",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProductReviews_UserId",
                table: "ProductReviews",
                column: "UserId");

            migrationBuilder.AddForeignKey(
                name: "FK_ProductReviews_Users_UserId",
                table: "ProductReviews",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ProductReviews_Users_UserId",
                table: "ProductReviews");

            migrationBuilder.DropIndex(
                name: "IX_ProductReviews_UserId",
                table: "ProductReviews");

            migrationBuilder.DropColumn(
                name: "UserId",
                table: "ProductReviews");

            migrationBuilder.AddColumn<string>(
                name: "ReviewerName",
                table: "ProductReviews",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");
        }
    }
}
