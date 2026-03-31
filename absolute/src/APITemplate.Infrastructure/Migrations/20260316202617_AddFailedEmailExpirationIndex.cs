using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace APITemplate.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddFailedEmailExpirationIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_FailedEmails_IsDeadLettered_CreatedAtUtc",
                table: "FailedEmails",
                columns: new[] { "IsDeadLettered", "CreatedAtUtc" }
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_FailedEmails_IsDeadLettered_CreatedAtUtc",
                table: "FailedEmails"
            );
        }
    }
}
