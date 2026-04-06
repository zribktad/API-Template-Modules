using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Notifications.Persistence.Migrations;

/// <inheritdoc />
public partial class InitialNotifications : Migration
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
                TemplateName = table.Column<string>(
                    type: "character varying(100)",
                    maxLength: 100,
                    nullable: true
                ),
                IsDeadLettered = table.Column<bool>(type: "boolean", nullable: false),
                ClaimedBy = table.Column<string>(
                    type: "character varying(200)",
                    maxLength: 200,
                    nullable: true
                ),
                ClaimedAtUtc = table.Column<DateTime>(
                    type: "timestamp with time zone",
                    nullable: true
                ),
                ClaimedUntilUtc = table.Column<DateTime>(
                    type: "timestamp with time zone",
                    nullable: true
                ),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_FailedEmails", x => x.Id);
            }
        );

        migrationBuilder.CreateIndex(
            name: "IX_FailedEmails_IsDeadLettered_ClaimedUntilUtc_CreatedAtUtc",
            table: "FailedEmails",
            columns: new[] { "IsDeadLettered", "ClaimedUntilUtc", "CreatedAtUtc" }
        );

        migrationBuilder.CreateIndex(
            name: "IX_FailedEmails_IsDeadLettered_RetryCount_ClaimedUntilUtc_Last~",
            table: "FailedEmails",
            columns: new[] { "IsDeadLettered", "RetryCount", "ClaimedUntilUtc", "LastAttemptAtUtc" }
        );
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "FailedEmails");
    }
}
