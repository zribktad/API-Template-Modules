using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace APITemplate.Migrations
{
    /// <inheritdoc />
    public partial class SoftDeleteProductDataLinksAndMetadata : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ProductDataLinks_Products_ProductId",
                table: "ProductDataLinks");

            migrationBuilder.DropIndex(
                name: "IX_ProductDataLinks_ProductDataId",
                table: "ProductDataLinks");

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAtUtc",
                table: "ProductDataLinks",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "now()");

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedBy",
                table: "ProductDataLinks",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAtUtc",
                table: "ProductDataLinks",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "DeletedBy",
                table: "ProductDataLinks",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "ProductDataLinks",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "ProductDataLinks",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAtUtc",
                table: "ProductDataLinks",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "now()");

            migrationBuilder.AddColumn<Guid>(
                name: "UpdatedBy",
                table: "ProductDataLinks",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<uint>(
                name: "xmin",
                table: "ProductDataLinks",
                type: "xid",
                rowVersion: true,
                nullable: false,
                defaultValue: 0u);

            migrationBuilder.CreateIndex(
                name: "IX_ProductDataLinks_TenantId",
                table: "ProductDataLinks",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductDataLinks_TenantId_IsDeleted",
                table: "ProductDataLinks",
                columns: new[] { "TenantId", "IsDeleted" });

            migrationBuilder.CreateIndex(
                name: "IX_ProductDataLinks_TenantId_ProductDataId_IsDeleted",
                table: "ProductDataLinks",
                columns: new[] { "TenantId", "ProductDataId", "IsDeleted" });

            migrationBuilder.AddCheckConstraint(
                name: "CK_ProductDataLinks_SoftDeleteConsistency",
                table: "ProductDataLinks",
                sql: "\"IsDeleted\" OR (\"DeletedAtUtc\" IS NULL AND \"DeletedBy\" IS NULL)");

            migrationBuilder.AddForeignKey(
                name: "FK_ProductDataLinks_Products_ProductId",
                table: "ProductDataLinks",
                column: "ProductId",
                principalTable: "Products",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ProductDataLinks_Products_ProductId",
                table: "ProductDataLinks");

            migrationBuilder.DropIndex(
                name: "IX_ProductDataLinks_TenantId",
                table: "ProductDataLinks");

            migrationBuilder.DropIndex(
                name: "IX_ProductDataLinks_TenantId_IsDeleted",
                table: "ProductDataLinks");

            migrationBuilder.DropIndex(
                name: "IX_ProductDataLinks_TenantId_ProductDataId_IsDeleted",
                table: "ProductDataLinks");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ProductDataLinks_SoftDeleteConsistency",
                table: "ProductDataLinks");

            migrationBuilder.DropColumn(
                name: "CreatedAtUtc",
                table: "ProductDataLinks");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "ProductDataLinks");

            migrationBuilder.DropColumn(
                name: "DeletedAtUtc",
                table: "ProductDataLinks");

            migrationBuilder.DropColumn(
                name: "DeletedBy",
                table: "ProductDataLinks");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "ProductDataLinks");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "ProductDataLinks");

            migrationBuilder.DropColumn(
                name: "UpdatedAtUtc",
                table: "ProductDataLinks");

            migrationBuilder.DropColumn(
                name: "UpdatedBy",
                table: "ProductDataLinks");

            migrationBuilder.DropColumn(
                name: "xmin",
                table: "ProductDataLinks");

            migrationBuilder.CreateIndex(
                name: "IX_ProductDataLinks_ProductDataId",
                table: "ProductDataLinks",
                column: "ProductDataId");

            migrationBuilder.AddForeignKey(
                name: "FK_ProductDataLinks_Products_ProductId",
                table: "ProductDataLinks",
                column: "ProductId",
                principalTable: "Products",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
