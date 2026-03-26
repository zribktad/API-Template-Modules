using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace APITemplate.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddFailedEmailClaims : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_FailedEmails_IsDeadLettered_CreatedAtUtc",
                table: "FailedEmails"
            );

            migrationBuilder.DropIndex(
                name: "IX_FailedEmails_IsDeadLettered_RetryCount_LastAttemptAtUtc",
                table: "FailedEmails"
            );

            migrationBuilder.AddColumn<DateTime>(
                name: "ClaimedAtUtc",
                table: "FailedEmails",
                type: "timestamp with time zone",
                nullable: true
            );

            migrationBuilder.AddColumn<string>(
                name: "ClaimedBy",
                table: "FailedEmails",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true
            );

            migrationBuilder.AddColumn<DateTime>(
                name: "ClaimedUntilUtc",
                table: "FailedEmails",
                type: "timestamp with time zone",
                nullable: true
            );

            migrationBuilder.CreateIndex(
                name: "IX_FailedEmails_IsDeadLettered_ClaimedUntilUtc_CreatedAtUtc",
                table: "FailedEmails",
                columns: new[] { "IsDeadLettered", "ClaimedUntilUtc", "CreatedAtUtc" }
            );

            migrationBuilder.CreateIndex(
                name: "IX_FailedEmails_IsDeadLettered_RetryCount_ClaimedUntilUtc_Last~",
                table: "FailedEmails",
                columns: new[]
                {
                    "IsDeadLettered",
                    "RetryCount",
                    "ClaimedUntilUtc",
                    "LastAttemptAtUtc",
                }
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_FailedEmails_IsDeadLettered_ClaimedUntilUtc_CreatedAtUtc",
                table: "FailedEmails"
            );

            migrationBuilder.DropIndex(
                name: "IX_FailedEmails_IsDeadLettered_RetryCount_ClaimedUntilUtc_Last~",
                table: "FailedEmails"
            );

            migrationBuilder.DropColumn(name: "ClaimedAtUtc", table: "FailedEmails");

            migrationBuilder.DropColumn(name: "ClaimedBy", table: "FailedEmails");

            migrationBuilder.DropColumn(name: "ClaimedUntilUtc", table: "FailedEmails");

            migrationBuilder.CreateIndex(
                name: "IX_FailedEmails_IsDeadLettered_CreatedAtUtc",
                table: "FailedEmails",
                columns: new[] { "IsDeadLettered", "CreatedAtUtc" }
            );

            migrationBuilder.CreateIndex(
                name: "IX_FailedEmails_IsDeadLettered_RetryCount_LastAttemptAtUtc",
                table: "FailedEmails",
                columns: new[] { "IsDeadLettered", "RetryCount", "LastAttemptAtUtc" }
            );
        }
    }
}
