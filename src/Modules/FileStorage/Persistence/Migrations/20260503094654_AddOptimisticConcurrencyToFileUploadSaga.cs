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
                table: "file_upload_sagas",
                schema: "file_storage",
                type: "xid",
                rowVersion: true,
                nullable: false
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "xmin",
                table: "file_upload_sagas",
                schema: "file_storage"
            );
        }
    }
}
