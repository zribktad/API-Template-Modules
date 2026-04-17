using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FileStorage.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCasSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(name: "file_storage");

            migrationBuilder.CreateTable(
                name: "file_upload_sagas",
                schema: "file_storage",
                columns: table => new
                {
                    Id = table.Column<string>(
                        type: "character varying(64)",
                        maxLength: 64,
                        nullable: false
                    ),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Sha256 = table.Column<string>(
                        type: "character(64)",
                        fixedLength: true,
                        maxLength: 64,
                        nullable: false
                    ),
                    SizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    OriginalFileName = table.Column<string>(
                        type: "character varying(255)",
                        maxLength: 255,
                        nullable: false
                    ),
                    StagingPath = table.Column<string>(
                        type: "character varying(1000)",
                        maxLength: 1000,
                        nullable: false
                    ),
                    BackendKey = table.Column<string>(
                        type: "character varying(32)",
                        maxLength: 32,
                        nullable: false
                    ),
                    Status = table.Column<string>(
                        type: "character varying(16)",
                        maxLength: 16,
                        nullable: false
                    ),
                    CreatedAtUtc = table.Column<DateTime>(
                        type: "timestamp with time zone",
                        nullable: false
                    ),
                    CommitDeadlineUtc = table.Column<DateTime>(
                        type: "timestamp with time zone",
                        nullable: false
                    ),
                    StoredFileId = table.Column<Guid>(type: "uuid", nullable: true),
                    Version = table.Column<int>(type: "integer", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_file_upload_sagas", x => x.Id);
                }
            );

            migrationBuilder.CreateTable(
                name: "stored_files",
                schema: "file_storage",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OriginalFileName = table.Column<string>(
                        type: "character varying(255)",
                        maxLength: 255,
                        nullable: false
                    ),
                    Sha256 = table.Column<string>(
                        type: "character(64)",
                        fixedLength: true,
                        maxLength: 64,
                        nullable: false
                    ),
                    BackendKey = table.Column<string>(
                        type: "character varying(32)",
                        maxLength: 32,
                        nullable: false
                    ),
                    ContentType = table.Column<string>(
                        type: "character varying(100)",
                        maxLength: 100,
                        nullable: false
                    ),
                    SizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    Description = table.Column<string>(
                        type: "character varying(500)",
                        maxLength: 500,
                        nullable: true
                    ),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(
                        type: "timestamp with time zone",
                        nullable: false,
                        defaultValueSql: "now()"
                    ),
                    CreatedBy = table.Column<Guid>(
                        type: "uuid",
                        nullable: false,
                        defaultValue: new Guid("00000000-0000-0000-0000-000000000000")
                    ),
                    UpdatedAtUtc = table.Column<DateTime>(
                        type: "timestamp with time zone",
                        nullable: false,
                        defaultValueSql: "now()"
                    ),
                    UpdatedBy = table.Column<Guid>(
                        type: "uuid",
                        nullable: false,
                        defaultValue: new Guid("00000000-0000-0000-0000-000000000000")
                    ),
                    IsDeleted = table.Column<bool>(
                        type: "boolean",
                        nullable: false,
                        defaultValue: false
                    ),
                    DeletedAtUtc = table.Column<DateTime>(
                        type: "timestamp with time zone",
                        nullable: true
                    ),
                    DeletedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_stored_files", x => x.Id);
                    table.CheckConstraint(
                        "CK_stored_files_SoftDeleteConsistency",
                        "\"IsDeleted\" OR (\"DeletedAtUtc\" IS NULL AND \"DeletedBy\" IS NULL)"
                    );
                }
            );

            migrationBuilder.CreateIndex(
                name: "IX_file_upload_sagas_CommitDeadlineUtc",
                schema: "file_storage",
                table: "file_upload_sagas",
                column: "CommitDeadlineUtc"
            );

            migrationBuilder.CreateIndex(
                name: "IX_file_upload_sagas_Status",
                schema: "file_storage",
                table: "file_upload_sagas",
                column: "Status"
            );

            migrationBuilder.CreateIndex(
                name: "IX_stored_files_Sha256_TenantId",
                schema: "file_storage",
                table: "stored_files",
                columns: new[] { "Sha256", "TenantId" }
            );

            migrationBuilder.CreateIndex(
                name: "IX_stored_files_TenantId",
                schema: "file_storage",
                table: "stored_files",
                column: "TenantId"
            );

            migrationBuilder.CreateIndex(
                name: "IX_stored_files_TenantId_IsDeleted",
                schema: "file_storage",
                table: "stored_files",
                columns: new[] { "TenantId", "IsDeleted" }
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "file_upload_sagas", schema: "file_storage");

            migrationBuilder.DropTable(name: "stored_files", schema: "file_storage");
        }
    }
}
