using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace APITemplate.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddFailedEmails : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "FailedEmails",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    To = table.Column<string>(
                        type: "character varying(320)",
                        maxLength: 320,
                        nullable: false
                    ),
                    Subject = table.Column<string>(
                        type: "character varying(500)",
                        maxLength: 500,
                        nullable: false
                    ),
                    HtmlBody = table.Column<string>(type: "text", nullable: false),
                    RetryCount = table.Column<int>(type: "integer", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(
                        type: "timestamp with time zone",
                        nullable: false
                    ),
                    LastAttemptAtUtc = table.Column<DateTime>(
                        type: "timestamp with time zone",
                        nullable: true
                    ),
                    LastError = table.Column<string>(
                        type: "character varying(2000)",
                        maxLength: 2000,
                        nullable: true
                    ),
                    IsDeadLettered = table.Column<bool>(type: "boolean", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FailedEmails", x => x.Id);
                }
            );

            migrationBuilder.CreateIndex(
                name: "IX_FailedEmails_IsDeadLettered_RetryCount_LastAttemptAtUtc",
                table: "FailedEmails",
                columns: new[] { "IsDeadLettered", "RetryCount", "LastAttemptAtUtc" }
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "FailedEmails");
        }
    }
}
