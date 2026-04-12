using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Identity.Migrations
{
    /// <inheritdoc />
    public partial class DynamicRolesAndPermissions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(name: "identity");

            migrationBuilder.CreateTable(
                name: "CustomRole",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    Name = table.Column<string>(
                        type: "character varying(100)",
                        maxLength: 100,
                        nullable: false
                    ),
                    IsImmutable = table.Column<bool>(type: "boolean", nullable: false),
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
                    table.PrimaryKey("PK_CustomRole", x => x.Id);
                    table.CheckConstraint(
                        "CK_CustomRole_SoftDeleteConsistency",
                        "\"IsDeleted\" OR (\"DeletedAtUtc\" IS NULL AND \"DeletedBy\" IS NULL)"
                    );
                }
            );

            migrationBuilder.CreateTable(
                name: "AppUserRoles",
                schema: "identity",
                columns: table => new
                {
                    RolesId = table.Column<Guid>(type: "uuid", nullable: false),
                    UsersId = table.Column<Guid>(type: "uuid", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppUserRoles", x => new { x.RolesId, x.UsersId });
                    table.ForeignKey(
                        name: "FK_AppUserRoles_CustomRole_RolesId",
                        column: x => x.RolesId,
                        principalTable: "CustomRole",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade
                    );
                    table.ForeignKey(
                        name: "FK_AppUserRoles_Users_UsersId",
                        column: x => x.UsersId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade
                    );
                }
            );

            migrationBuilder.CreateTable(
                name: "RolePermissions",
                schema: "identity",
                columns: table => new
                {
                    RoleId = table.Column<Guid>(type: "uuid", nullable: false),
                    Permission = table.Column<string>(
                        type: "character varying(100)",
                        maxLength: 100,
                        nullable: false
                    ),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RolePermissions", x => new { x.RoleId, x.Permission });
                    table.ForeignKey(
                        name: "FK_RolePermissions_CustomRole_RoleId",
                        column: x => x.RoleId,
                        principalTable: "CustomRole",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade
                    );
                }
            );

            migrationBuilder.CreateIndex(
                name: "IX_AppUserRoles_UsersId",
                schema: "identity",
                table: "AppUserRoles",
                column: "UsersId"
            );

            migrationBuilder.CreateIndex(
                name: "IX_CustomRole_TenantId",
                table: "CustomRole",
                column: "TenantId"
            );

            migrationBuilder.CreateIndex(
                name: "IX_CustomRole_TenantId_Name",
                table: "CustomRole",
                columns: new[] { "TenantId", "Name" },
                unique: true
            );

            // Data Migration: Seed Default Roles
            var platformAdminId = Guid.NewGuid();
            var tenantAdminId = Guid.NewGuid();
            var userId = Guid.NewGuid();

            migrationBuilder.InsertData(
                table: "CustomRole",
                columns: new[] { "Id", "TenantId", "Name", "IsImmutable" },
                values: new object[,]
                {
                    { platformAdminId, null, "PlatformAdmin", true },
                    { tenantAdminId, null, "TenantAdmin", true },
                    { userId, null, "User", true },
                }
            );

            // Data Migration: Seed Permissions
            migrationBuilder.Sql(
                $@"
                INSERT INTO identity.""RolePermissions"" (""RoleId"", ""Permission"") VALUES ('{platformAdminId}', 'Platform.Manage');
                INSERT INTO identity.""RolePermissions"" (""RoleId"", ""Permission"") VALUES ('{tenantAdminId}', 'Tenant.Manage');
            "
            );

            // Data Migration: Migrate existing users
            migrationBuilder.Sql(
                $@"
                INSERT INTO identity.""AppUserRoles"" (""UsersId"", ""RolesId"")
                SELECT ""Id"", 
                       CASE 
                           WHEN ""Role"" = 'PlatformAdmin' THEN '{platformAdminId}'::uuid
                           WHEN ""Role"" = 'TenantAdmin' THEN '{tenantAdminId}'::uuid
                           ELSE '{userId}'::uuid
                       END
                FROM ""Users"";
            "
            );

            migrationBuilder.DropColumn(name: "Role", table: "Users");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Role",
                table: "Users",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "User"
            );

            // Restore roles from Many-to-Many mapping
            migrationBuilder.Sql(
                @"
                UPDATE ""Users""
                SET ""Role"" = (
                    SELECT cr.""Name""
                    FROM identity.""AppUserRoles"" ur
                    JOIN ""CustomRole"" cr ON cr.""Id"" = ur.""RolesId""
                    WHERE ur.""UsersId"" = ""Users"".""Id""
                    LIMIT 1
                )
            "
            );

            migrationBuilder.DropTable(name: "AppUserRoles", schema: "identity");

            migrationBuilder.DropTable(name: "RolePermissions", schema: "identity");

            migrationBuilder.DropTable(name: "CustomRole");
        }
    }
}
