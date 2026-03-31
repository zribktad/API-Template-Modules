using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace APITemplate.Migrations
{
    /// <inheritdoc />
    public partial class AddFullTextSearchIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_Products_Name_Description",
                table: "Products",
                columns: new[] { "Name", "Description" })
                .Annotation("Npgsql:IndexMethod", "GIN")
                .Annotation("Npgsql:TsVectorConfig", "english");

            migrationBuilder.CreateIndex(
                name: "IX_Categories_Name_Description",
                table: "Categories",
                columns: new[] { "Name", "Description" })
                .Annotation("Npgsql:IndexMethod", "GIN")
                .Annotation("Npgsql:TsVectorConfig", "english");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Products_Name_Description",
                table: "Products");

            migrationBuilder.DropIndex(
                name: "IX_Categories_Name_Description",
                table: "Categories");
        }
    }
}
