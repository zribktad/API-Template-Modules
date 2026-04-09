using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Identity.Migrations
{
    /// <inheritdoc />
    public partial class AddProvisioningStatusToAppUser : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Tenants",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(
                        type: "character varying(100)",
                        maxLength: 100,
                        nullable: false
                    ),
                    Name = table.Column<string>(
                        type: "character varying(200)",
                        maxLength: 200,
                        nullable: false
                    ),
                    IsActive = table.Column<bool>(
                        type: "boolean",
                        nullable: false,
                        defaultValue: true
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
                    table.PrimaryKey("PK_Tenants", x => x.Id);
                    table.CheckConstraint(
                        "CK_Tenants_SoftDeleteConsistency",
                        "\"IsDeleted\" OR (\"DeletedAtUtc\" IS NULL AND \"DeletedBy\" IS NULL)"
                    );
                }
            );

            migrationBuilder.CreateTable(
                name: "TenantInvitations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Email = table.Column<string>(
                        type: "character varying(320)",
                        maxLength: 320,
                        nullable: false
                    ),
                    NormalizedEmail = table.Column<string>(
                        type: "character varying(320)",
                        maxLength: 320,
                        nullable: false
                    ),
                    TokenHash = table.Column<string>(
                        type: "character varying(128)",
                        maxLength: 128,
                        nullable: false
                    ),
                    ExpiresAtUtc = table.Column<DateTime>(
                        type: "timestamp with time zone",
                        nullable: false
                    ),
                    Status = table.Column<string>(
                        type: "character varying(32)",
                        maxLength: 32,
                        nullable: false,
                        defaultValue: "Pending"
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
                    table.PrimaryKey("PK_TenantInvitations", x => x.Id);
                    table.CheckConstraint(
                        "CK_TenantInvitations_SoftDeleteConsistency",
                        "\"IsDeleted\" OR (\"DeletedAtUtc\" IS NULL AND \"DeletedBy\" IS NULL)"
                    );
                    table.ForeignKey(
                        name: "FK_TenantInvitations_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict
                    );
                }
            );

            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Username = table.Column<string>(
                        type: "character varying(100)",
                        maxLength: 100,
                        nullable: false
                    ),
                    NormalizedUsername = table.Column<string>(
                        type: "character varying(100)",
                        maxLength: 100,
                        nullable: false
                    ),
                    Email = table.Column<string>(
                        type: "character varying(320)",
                        maxLength: 320,
                        nullable: false
                    ),
                    NormalizedEmail = table.Column<string>(
                        type: "character varying(320)",
                        maxLength: 320,
                        nullable: false
                    ),
                    KeycloakUserId = table.Column<string>(
                        type: "character varying(256)",
                        maxLength: 256,
                        nullable: true
                    ),
                    IsActive = table.Column<bool>(
                        type: "boolean",
                        nullable: false,
                        defaultValue: true
                    ),
                    Role = table.Column<string>(
                        type: "character varying(32)",
                        maxLength: 32,
                        nullable: false,
                        defaultValue: "User"
                    ),
                    ProvisioningStatus = table.Column<string>(
                        type: "character varying(32)",
                        maxLength: 32,
                        nullable: false,
                        defaultValue: "Pending"
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
                    table.PrimaryKey("PK_Users", x => x.Id);
                    table.CheckConstraint(
                        "CK_Users_SoftDeleteConsistency",
                        "\"IsDeleted\" OR (\"DeletedAtUtc\" IS NULL AND \"DeletedBy\" IS NULL)"
                    );
                    table.ForeignKey(
                        name: "FK_Users_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict
                    );
                }
            );

            migrationBuilder.CreateIndex(
                name: "IX_TenantInvitations_TenantId",
                table: "TenantInvitations",
                column: "TenantId"
            );

            migrationBuilder.CreateIndex(
                name: "IX_TenantInvitations_TenantId_IsDeleted",
                table: "TenantInvitations",
                columns: new[] { "TenantId", "IsDeleted" }
            );

            migrationBuilder.CreateIndex(
                name: "IX_TenantInvitations_TenantId_NormalizedEmail",
                table: "TenantInvitations",
                columns: new[] { "TenantId", "NormalizedEmail" }
            );

            migrationBuilder.CreateIndex(
                name: "IX_TenantInvitations_TokenHash",
                table: "TenantInvitations",
                column: "TokenHash"
            );

            migrationBuilder.CreateIndex(
                name: "IX_Tenants_Code",
                table: "Tenants",
                column: "Code",
                unique: true
            );

            migrationBuilder.CreateIndex(
                name: "IX_Tenants_IsActive",
                table: "Tenants",
                column: "IsActive"
            );

            migrationBuilder.CreateIndex(
                name: "IX_Tenants_TenantId",
                table: "Tenants",
                column: "TenantId"
            );

            migrationBuilder.CreateIndex(
                name: "IX_Tenants_TenantId_IsDeleted",
                table: "Tenants",
                columns: new[] { "TenantId", "IsDeleted" }
            );

            migrationBuilder.CreateIndex(
                name: "IX_Users_KeycloakUserId",
                table: "Users",
                column: "KeycloakUserId",
                unique: true,
                filter: "\"KeycloakUserId\" IS NOT NULL"
            );

            migrationBuilder.CreateIndex(
                name: "IX_Users_TenantId",
                table: "Users",
                column: "TenantId"
            );

            migrationBuilder.CreateIndex(
                name: "IX_Users_TenantId_IsDeleted",
                table: "Users",
                columns: new[] { "TenantId", "IsDeleted" }
            );

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
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "TenantInvitations");

            migrationBuilder.DropTable(name: "Users");

            migrationBuilder.DropTable(name: "Tenants");
        }
    }
}
