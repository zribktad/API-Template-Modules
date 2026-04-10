using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Identity.Migrations
{
    /// <inheritdoc />
    public partial class AddBffPersistedSession : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BffSessions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SessionId = table.Column<string>(
                        type: "character varying(128)",
                        maxLength: 128,
                        nullable: false
                    ),
                    UserId = table.Column<string>(
                        type: "character varying(256)",
                        maxLength: 256,
                        nullable: false
                    ),
                    Subject = table.Column<string>(
                        type: "character varying(256)",
                        maxLength: 256,
                        nullable: false
                    ),
                    Provider = table.Column<string>(
                        type: "character varying(32)",
                        maxLength: 32,
                        nullable: false,
                        defaultValue: "Keycloak"
                    ),
                    Roles = table.Column<string>(
                        type: "jsonb",
                        nullable: false,
                        defaultValueSql: "'[]'::jsonb"
                    ),
                    Email = table.Column<string>(
                        type: "character varying(320)",
                        maxLength: 320,
                        nullable: true
                    ),
                    DisplayName = table.Column<string>(
                        type: "character varying(256)",
                        maxLength: 256,
                        nullable: true
                    ),
                    EncryptedAccessToken = table.Column<string>(type: "text", nullable: false),
                    EncryptedRefreshToken = table.Column<string>(type: "text", nullable: false),
                    EncryptedIdToken = table.Column<string>(type: "text", nullable: true),
                    AccessTokenExpiresAtUtc = table.Column<DateTimeOffset>(
                        type: "timestamp with time zone",
                        nullable: false
                    ),
                    RefreshTokenExpiresAtUtc = table.Column<DateTimeOffset>(
                        type: "timestamp with time zone",
                        nullable: true
                    ),
                    SessionCreatedAtUtc = table.Column<DateTimeOffset>(
                        type: "timestamp with time zone",
                        nullable: false
                    ),
                    LastSeenAtUtc = table.Column<DateTimeOffset>(
                        type: "timestamp with time zone",
                        nullable: false
                    ),
                    LastRefreshedAtUtc = table.Column<DateTimeOffset>(
                        type: "timestamp with time zone",
                        nullable: false
                    ),
                    Status = table.Column<string>(
                        type: "character varying(32)",
                        maxLength: 32,
                        nullable: false,
                        defaultValue: "Active"
                    ),
                    RevokedAtUtc = table.Column<DateTimeOffset>(
                        type: "timestamp with time zone",
                        nullable: true
                    ),
                    Version = table.Column<long>(type: "bigint", nullable: false),
                    RevocationReason = table.Column<string>(
                        type: "character varying(64)",
                        maxLength: 64,
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
                    table.PrimaryKey("PK_BffSessions", x => x.Id);
                    table.CheckConstraint(
                        "CK_BffSessions_SoftDeleteConsistency",
                        "\"IsDeleted\" OR (\"DeletedAtUtc\" IS NULL AND \"DeletedBy\" IS NULL)"
                    );
                }
            );

            migrationBuilder.CreateIndex(
                name: "IX_BffSessions_SessionId",
                table: "BffSessions",
                column: "SessionId",
                unique: true
            );

            migrationBuilder.CreateIndex(
                name: "IX_BffSessions_Status_LastSeenAtUtc",
                table: "BffSessions",
                columns: new[] { "Status", "LastSeenAtUtc" }
            );

            migrationBuilder.CreateIndex(
                name: "IX_BffSessions_TenantId",
                table: "BffSessions",
                column: "TenantId"
            );

            migrationBuilder.CreateIndex(
                name: "IX_BffSessions_TenantId_IsDeleted",
                table: "BffSessions",
                columns: new[] { "TenantId", "IsDeleted" }
            );

            migrationBuilder.CreateIndex(
                name: "IX_BffSessions_UserId",
                table: "BffSessions",
                column: "UserId"
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "BffSessions");
        }
    }
}
