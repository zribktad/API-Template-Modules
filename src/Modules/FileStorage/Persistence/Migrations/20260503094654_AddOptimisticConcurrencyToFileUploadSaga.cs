using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FileStorage.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddOptimisticConcurrencyToFileUploadSaga : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<uint>(
                name: "xmin",
                schema: "file_storage",
                table: "file_upload_sagas",
                type: "xid",
                rowVersion: true,
                nullable: false,
                defaultValue: 0u
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "xmin",
                schema: "file_storage",
                table: "file_upload_sagas"
            );
        }
    }
}
