using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace APITemplate.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddFailedEmailTemplateName : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "TemplateName",
                table: "FailedEmails",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "TemplateName", table: "FailedEmails");
        }
    }
}
